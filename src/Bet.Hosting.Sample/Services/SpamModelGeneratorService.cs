﻿using System;
using System.IO;
using System.Threading.Tasks;

using Bet.Extensions.ML.ModelBuilder;
using Bet.Extensions.ML.Spam.Models;

using Microsoft.Extensions.Logging;
using Microsoft.ML;

namespace Bet.Hosting.Sample.Services
{
    public class SpamModelGeneratorService
    {
        private readonly ILogger<SpamModelGeneratorService> _logger;
        private readonly IModelCreationBuilder<SpamInput, SpamPrediction, MulticlassClassificationFoldsAverageMetricsResult> _modelBuilder;
        private readonly ModelPathService _pathService;

        public SpamModelGeneratorService(
            IModelCreationBuilder<SpamInput, SpamPrediction, MulticlassClassificationFoldsAverageMetricsResult> spamModelBuilder,
            ModelPathService pathService,
            ILogger<SpamModelGeneratorService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modelBuilder = spamModelBuilder ?? throw new ArgumentNullException(nameof(spamModelBuilder));
            _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        }

        public Task GenerateModel()
        {
            // 1. load default ML data set
            _logger.LogInformation("=============== Loading data===============");
            _modelBuilder.LoadDefaultData()
                .BuiltDataView();

            // 2. build training pipeline
            _logger.LogInformation("=============== BuildTrainingPipeline ===============");
            var buildTrainingPipelineResult = _modelBuilder.BuildTrainingPipeline();
            _logger.LogInformation("BuildTrainingPipeline ran for: {BuildTrainingPipelineTime}", buildTrainingPipelineResult.ElapsedMilliseconds);

            // 3. evaluate quality of the pipeline
            _logger.LogInformation("=============== Evaluate ===============");
            var evaluateResult = _modelBuilder.Evaluate();
            _logger.LogInformation("Evaluate ran for {EvaluateTime}", evaluateResult.ElapsedMilliseconds);
            _logger.LogInformation(evaluateResult.ToString());

            // 4. train the model
            _logger.LogInformation("=============== TrainModel ===============");
            var trainModelResult = _modelBuilder.TrainModel();
            _logger.LogInformation("TrainModel ran for {TrainModelTime}", trainModelResult.ElapsedMilliseconds);

            // 5. predict on sample data
            _logger.LogInformation("=============== Predictions for below data===============");

            var predictor =_modelBuilder.MLContext.Model.CreatePredictionEngine<SpamInput, SpamPrediction>(_modelBuilder.Model);
            // Test a few examples
            ClassifySpamMessage(predictor, "That's a great idea. It should work.");
            ClassifySpamMessage(predictor, "free medicine winner! congratulations");
            ClassifySpamMessage(predictor, "Yes we should meet over the weekend!");
            ClassifySpamMessage(predictor, "you win pills and free entry vouchers");

            // 6. save to the file
            _logger.LogInformation("=================== Saving Model to Disk ============================ ");

            _modelBuilder.SaveModel(_pathService.SpamModelPath);

            _logger.LogInformation("======================= Creating Model Completed ================== ");

            return Task.CompletedTask;
        }

        private void ClassifySpamMessage(PredictionEngine<SpamInput, SpamPrediction> predictor, string message)
        {
            var input = new SpamInput { Message = message };
            var prediction = predictor.Predict(input);
            _logger.LogInformation("The message '{0}' is {1}", input.Message, prediction.IsSpam == "spam" ? "spam" : "not spam");
        }
    }
}