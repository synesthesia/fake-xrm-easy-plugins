using System;
using FakeXrmEasy.Abstractions;
using FakeXrmEasy.Abstractions.Middleware;
using FakeXrmEasy.Abstractions.Plugins.Enums;
using Microsoft.Xrm.Sdk;

using FakeXrmEasy.Pipeline;
using FakeXrmEasy.Extensions;
using FakeXrmEasy.Plugins.PluginImages;
using FakeXrmEasy.Plugins.Audit;
using FakeXrmEasy.Plugins.PluginSteps;

namespace FakeXrmEasy.Middleware.Pipeline
{
    /// <summary>
    /// Provides extensions for plugin pipeline simulation
    /// </summary>
    public static class MiddlewareBuilderPipelineExtensions
    {
        /// <summary>
        /// Enables Pipeline Simulation in middleware with default options
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IMiddlewareBuilder AddPipelineSimulation(this IMiddlewareBuilder builder) 
        {
            return builder.AddPipelineSimulation(new PipelineOptions());
        }

        /// <summary>
        /// Enables Piepeline Simulation in middleware with custom options
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="options">The custom options</param>
        /// <returns></returns>
        public static IMiddlewareBuilder AddPipelineSimulation(this IMiddlewareBuilder builder, PipelineOptions options)
        {
            builder.Add(context => {
                context.SetProperty(options);

                if(options.UsePluginStepAudit)
                {
                    context.SetProperty<IPluginStepAudit>(new PluginStepAudit());
                }

                if(options.UsePluginStepRegistrationValidation)
                {
                    context.SetProperty<IPluginStepValidator>(new PluginStepValidator());
                }
            });

            return builder;
        }

        /// <summary>
        /// Inserts pipeline simulation into the middleware. When using pipeline simulation, this method should be called before all the other Use*() methods
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IMiddlewareBuilder UsePipelineSimulation(this IMiddlewareBuilder builder) 
        {
            Func<OrganizationRequestDelegate, OrganizationRequestDelegate> middleware = next => {

                return (IXrmFakedContext context, OrganizationRequest request) => {
                    
                    if(CanHandleRequest(context, request)) 
                    {
                        var preImagePreOperation = PreImage.IsAvailableFor(request.GetType(), ProcessingStepStage.Preoperation) ?
                                            GetPreImageEntityForRequest(context, request) : null;

                        var preImagePostOperation = PreImage.IsAvailableFor(request.GetType(), ProcessingStepStage.Postoperation) ?
                                            GetPreImageEntityForRequest(context, request) : null;


                        var target = IXrmFakedContextPipelineExtensions.GetTargetForRequest(request);

                        ProcessPreValidation(context, request, target);
                        ProcessPreOperation(context, request, target, preImagePreOperation);

                        var response = next.Invoke(context, request);


                        var postImagePostOperation = PostImage.IsAvailableFor(request.GetType(), ProcessingStepStage.Postoperation) ?
                                            GetPostImageEntityForRequest(context, request) : null;

                        ProcessPostOperation(context, request, target, preImagePostOperation, postImagePostOperation);
                        return response;
                    }
                    else 
                    {
                        return next.Invoke(context, request);
                    }
                };
            };
            
            builder.Use(middleware);
            return builder;
        }

        private static bool CanHandleRequest(IXrmFakedContext context, OrganizationRequest request) 
        {
            var pipelineOptions = context.GetProperty<PipelineOptions>();
            return pipelineOptions?.UsePipelineSimulation == true;
        }

        private static void ProcessPreValidation(IXrmFakedContext context, OrganizationRequest request, object target, Entity preEntity = null, Entity postEntity = null)
        {
            context.ExecutePipelineStage(request.RequestName, ProcessingStepStage.Prevalidation, ProcessingStepMode.Synchronous, request, target, preEntity, postEntity);
        }

        private static void ProcessPreOperation(IXrmFakedContext context, OrganizationRequest request, object target, Entity preEntity = null, Entity postEntity = null) 
        {
            context.ExecutePipelineStage(request.RequestName, ProcessingStepStage.Preoperation, ProcessingStepMode.Synchronous, request, target, preEntity, postEntity);
        }

        private static void ProcessPostOperation(IXrmFakedContext context, OrganizationRequest request, object target, Entity preEntity = null, Entity postEntity = null) 
        {
            context.ExecutePipelineStage(request.RequestName, ProcessingStepStage.Postoperation, ProcessingStepMode.Synchronous, request, target, preEntity, postEntity);
            context.ExecutePipelineStage(request.RequestName, ProcessingStepStage.Postoperation, ProcessingStepMode.Asynchronous, request, target, preEntity, postEntity);
        }

        private static Entity GetPreImageEntityForRequest(IXrmFakedContext context, OrganizationRequest request)
        {
            var target = IXrmFakedContextPipelineExtensions.GetTargetForRequest(request);
            if (target == null)
            {
                return null;
            }

            string logicalName = "";
            Guid id = Guid.Empty;

            if (target is Entity)
            {
                var targetEntity = target as Entity;
                logicalName = targetEntity.LogicalName;
                id = targetEntity.Id;
            }

            else if (target is EntityReference)
            {
                var targetEntityRef = target as EntityReference;
                logicalName = targetEntityRef.LogicalName;
                id = targetEntityRef.Id;
            }

            return context.GetEntityById(logicalName, id);
        }

        private static Entity GetPostImageEntityForRequest(IXrmFakedContext context, OrganizationRequest request)
        {
            var target = IXrmFakedContextPipelineExtensions.GetTargetForRequest(request);
            if (target == null)
            {
                return null;
            }

            if (target is Entity)
            {
                var targetEntity = target as Entity;
                return targetEntity;
            }

            else if (target is EntityReference)
            {
                return null;
            }

            return null;
        }
    }
}
