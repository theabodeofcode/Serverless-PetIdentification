﻿using AutoMapper;
using FluentAssertions;
using Moq;
using PetIdentification.Functions;
using PetIdentification.Interfaces;
using PetIdentification.Models;
using PetIdentification.Profiles;
using PetIdentification.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace PetIdentification.Tests.UnitTests
{
    public class ActivityFunctionsControllerTests
    {
        private readonly IMapper _mapper;
        private readonly Mock<IPredictionHelper> _predictionHelper;
        private readonly Mock<IAdoptionCentreDbHelper> _adoptionCentreDbHelper;
        private readonly Mock<IBreedInfoDbHelper> _breedInfoDbHelper;
        private readonly ActivityFunctionsController _funcController;

        public ActivityFunctionsControllerTests()
        {


            _mapper = InstanceFactory.CreateMapper();

            _predictionHelper = new Mock<IPredictionHelper>();

            _predictionHelper.Setup(
                x => x.PredictBreedAsync(It.IsAny<string>())
            )
            .ReturnsAsync(InstanceFactory.PredictedTags);

            _adoptionCentreDbHelper = new Mock<IAdoptionCentreDbHelper>();

            _adoptionCentreDbHelper.Setup(
                x => x.GetAdoptionCentresByBreedAsync(It.IsAny<string>())
            ).ReturnsAsync(InstanceFactory.AdoptionCentres);

            _breedInfoDbHelper = new Mock<IBreedInfoDbHelper>();

            _breedInfoDbHelper.Setup(
                x => x.GetBreedInformationAsync(It.IsAny<string>())
                ).ReturnsAsync(InstanceFactory.BreedInfo);

            _funcController = new ActivityFunctionsController(
                    adoptionCentreDbHelper: _adoptionCentreDbHelper.Object,
                    breedInfoDbHelper: _breedInfoDbHelper.Object,
                    predictionHelper: _predictionHelper.Object
                );

        }

        [Fact]
        public async Task Does_IdentifyStrayPetBreedAsync_Return_Prediction_Result()
        {

            var result = await _funcController
            .PredictStrayPetBreedAsync(string.Empty, InstanceFactory.CreateLogger(LoggerTypes.List));

            //Assertions

            result.Should().BeOfType<List<PredictionResult>>();
            result.Should().HaveCount(1);
            result[0].Probability.Should().Be(1.0);
            result[0].TagName.Should().Be("pug");

        }

        [Fact]
        public async Task Does_LocateAdoptionCentresByBreedAsync_Return_List_Of_AdoptionCentres()
        {
            var result = await _funcController
            .LocateAdoptionCentresByBreedAsync(string.Empty, InstanceFactory.CreateLogger(LoggerTypes.List));

            //Assertions

            result.Should().BeOfType<List<AdoptionCentre>>();
            result.Should().HaveCount(1);
            result[0].ShelteredBreed.Should().Be("pug");
        }

        [Fact]
        public async Task Does_GetBreedInformationAsync_Return_Breed_Information()
        {
            var logger = InstanceFactory.CreateLogger(LoggerTypes.List);

            var result = await _funcController
                .GetBreedInformationASync(string.Empty,
                InstanceFactory.CreateLogger());

            result.Should().BeOfType<BreedInfo>();
            (result as BreedInfo).Breed.Should().BeEquivalentTo("pug");

        }
    }
}
