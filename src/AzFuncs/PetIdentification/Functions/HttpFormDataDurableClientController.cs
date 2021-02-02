﻿using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PetIdentification.Constants;
using PetIdentification.Dtos;
using PetIdentification.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PetIdentification.Functions
{
    public class HttpFormDataDurableClientController
    {

        #region Properties&Fields

        private readonly IMapper _mapper;

        #endregion

        #region Constructors

        public HttpFormDataDurableClientController
        (
            IMapper mapper
        )
        {
            _mapper = mapper ??
            throw new ArgumentNullException(nameof(mapper));
        }

        #endregion

        #region Orchestration
        [FunctionName("HttpFormDataOrchestration")]
        public async Task<string> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger
        )
        {
            logger.LogInformation("Starting the execution of the orchestration: HttpFormDataOrchestration");

            var durableReqDto = context.GetInput<DurableRequestDto>();

            //var imageUrl = context.GetInput<string>();

            var predictions = await context.CallActivityAsync<List<PredictionResult>>
            (
                ActivityFunctionsConstants.IdentifyStrayPetBreedWithUrlAsync,
                durableReqDto.BlobUrl.ToString());

            var highestPrediction = predictions.OrderBy(x => x.Probability).FirstOrDefault();

            string tagName = highestPrediction.TagName;

            Task<List<AdoptionCentre>> getAdoptionCentres = context.CallActivityAsync<List<AdoptionCentre>>(
                    ActivityFunctionsConstants.LocateAdoptionCentresByBreedAsync,
                    tagName
                );

            Task<BreedInfo> getBreedInfo = context.CallActivityAsync<BreedInfo>(
                    ActivityFunctionsConstants.GetBreedInformationAsync,
                    tagName
                );

            await Task.WhenAll(getAdoptionCentres, getBreedInfo);

            var petIdentificationCanonical = new
                PetIdentificationCanonical
            {
                AdoptionCentres = getAdoptionCentres.Result,
                BreedInformation = getBreedInfo.Result
            };

            var petIdentificationCanonicalDto = _mapper
                .Map<PetIdentificationCanonical, PetIdentificationCanonicalDto>
                (petIdentificationCanonical);

            var signalRRequest = new SignalRRequest()
            {
                Message = JsonConvert.SerializeObject(petIdentificationCanonicalDto),
                UserId = durableReqDto.SignalRUserId
            };

            await context.CallActivityAsync("PushMessagesToSignalRHub", signalRRequest);

            logger.LogInformation("Finished execution of the orchestration: HttpFormDataOrchestration");

            return "Orchestrator HttpFormDataOrchestration executed the functions.";

        }

        #endregion

        #region DurableClient
        [FunctionName("HttpFormDataDurableClient")]
        public async Task<IActionResult> HttpUrlDurableClient(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest request,
            [DurableClient] IDurableClient durableClient,
            ILogger logger
        )
        {
            logger.LogInformation("Started the execution of the HttpFormDataDurableClient");
            if (!request.HasFormContentType)
            {
                return new UnsupportedMediaTypeResult();
            }

            var file = request.Form.Files[0];
            var signalRUserId = request.Form["SignalRUserId"];

            List<string> allowedFileExtensions = new List<string>()
            {
                "image/jpeg",
                "image/png"
            };

            if (!allowedFileExtensions.Contains(file.ContentType))
                return new BadRequestObjectResult("Only jpeg and png images are supported"); ;

            if (string.IsNullOrWhiteSpace(signalRUserId))
                return new BadRequestObjectResult("SignalRUserId field is mandatory");

            var requestBody = string.Empty;

            using (StreamReader reader = new StreamReader(request.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            var durableReqDto = JsonConvert.DeserializeObject<DurableRequestDto>(requestBody);


            await durableClient
                .StartNewAsync("HttpFormDataOrchestration", instanceId: new Guid().ToString(), durableReqDto);

            return new AcceptedResult();

        }

        #endregion



    }
}
