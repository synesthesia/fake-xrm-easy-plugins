﻿using Crm;
using FakeItEasy;
using FakeXrmEasy.Abstractions;
using FakeXrmEasy.Abstractions.Enums;
using FakeXrmEasy.Abstractions.Plugins.Enums;
using FakeXrmEasy.Middleware;
using FakeXrmEasy.Middleware.Crud;
using FakeXrmEasy.Middleware.Messages;
using FakeXrmEasy.Middleware.Pipeline;
using FakeXrmEasy.Pipeline;
using FakeXrmEasy.Plugins.Audit;
using FakeXrmEasy.Tests.PluginsForTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Linq;
using Xunit;

namespace FakeXrmEasy.Plugins.Tests.Pipeline
{
    public class PluginStepAuditInPipelineTests
    {
        private IXrmFakedContext _context;
        private IOrganizationService _service;

        private readonly Account _account;
        private readonly Contact _contact;

        public PluginStepAuditInPipelineTests()
        {
            _account = new Account()
            {
                Id = Guid.NewGuid(),
                AccountNumber = "1234567890",
                AccountCategoryCode = new OptionSetValue(1),
                NumberOfEmployees = 5,
                Revenue = new Money(20000),
                Telephone1 = "+123456"
            };

            _contact = new Contact()
            {
                Id = Guid.NewGuid()
            };
        }

        private IXrmFakedContext CreatePluginStepAuditEnabledContext()
        {
            return MiddlewareBuilder
                        .New()

                        // Add* -> Middleware configuration
                        .AddCrud()
                        .AddFakeMessageExecutors()
                        .AddPipelineSimulation(new PipelineOptions() { UsePluginStepAudit = true })

                        // Use* -> Defines pipeline sequence
                        .UsePipelineSimulation()
                        .UseMessages()
                        .UseCrud()
                        
                        .SetLicense(FakeXrmEasyLicense.RPL_1_5)
                        .Build();
        }

        private IXrmFakedContext CreatePluginStepAuditDisabledContext()
        {
            return MiddlewareBuilder
                        .New()

                        // Add* -> Middleware configuration
                        .AddCrud()
                        .AddFakeMessageExecutors()
                        .AddPipelineSimulation()

                        // Use* -> Defines pipeline sequence
                        .UsePipelineSimulation()
                        .UseMessages()
                        .UseCrud()
                        

                        .SetLicense(FakeXrmEasyLicense.RPL_1_5)
                        .Build();
        }

        [Theory]
        [InlineData(ProcessingStepStage.Prevalidation)]
        [InlineData(ProcessingStepStage.Preoperation)]
        [InlineData(ProcessingStepStage.Postoperation)]
        public void Should_capture_plugin_step_execution_for_several_stages_if_audit_is_enabled(ProcessingStepStage stage)
        {
            _context = CreatePluginStepAuditEnabledContext();
            _service = _context.GetOrganizationService();

            _context.RegisterPluginStep<AccountNumberPlugin>("Create", stage);

            var account = new Account() { Name = "Some name" };

            _service.Execute(new CreateRequest()
            {
                Target = account
            });

            var pluginStepAudit = _context.GetProperty<IPluginStepAudit>();
            var stepsAudit = pluginStepAudit.CreateQuery().ToList();

            Assert.Single(stepsAudit);

            var auditedStep = stepsAudit[0];

            Assert.Equal("Create", auditedStep.MessageName);
            Assert.Equal(stage, auditedStep.Stage);
            Assert.Equal(typeof(AccountNumberPlugin), auditedStep.PluginAssemblyType);
        }

        [Fact]
        public void Should_throw_exception_if_querying_plugin_step_audit_without_it_being_enabled_in_pipeline()
        {
            _context = CreatePluginStepAuditDisabledContext();
            _service = _context.GetOrganizationService();

            _context.RegisterPluginStep<AccountNumberPlugin>("Create", ProcessingStepStage.Preoperation);

            var account = new Account() { Name = "Some name" };

            _service.Execute(new CreateRequest()
            {
                Target = account
            });

            Assert.Throws<TypeAccessException>(() => _context.GetProperty<IPluginStepAudit>());
        }

        /*
            *  Will work once DynamicsValue/fake-xrm-easy#31 is implemented

        [Theory]
        [InlineData("Create", ProcessingStepStage.Prevalidation)]
        [InlineData("Create", ProcessingStepStage.Preoperation)]
        [InlineData("Create", ProcessingStepStage.Postoperation)]
        public void Should_capture_plugin_step_execution_for_several_stages_and_generic_requests_if_audit_is_enabled(string requestName, ProcessingStepStage stage)
        {
            _context = CreatePluginStepAuditEnabledContext();
            _service = _context.GetOrganizationService();

            _context.RegisterPluginStep<AccountNumberPlugin>("Create", stage);

            var account = new Account() { Name = "Some name" };

            _service.Execute(new OrganizationRequest()
            {
                RequestName = requestName,
                Parameters = new ParameterCollection
                {
                    { "Target", account }
                }
            });

            var pluginStepAudit = _context.GetProperty<IPluginStepAudit>();
            var stepsAudit = pluginStepAudit.CreateQuery().ToList();

            Assert.Single(stepsAudit);

            var auditedStep = stepsAudit[0];

            Assert.Equal(requestName, auditedStep.MessageName);
            Assert.Equal(stage, auditedStep.Stage);
            Assert.Equal(typeof(AccountNumberPlugin), auditedStep.PluginAssemblyType);
        }

        */
    }
}