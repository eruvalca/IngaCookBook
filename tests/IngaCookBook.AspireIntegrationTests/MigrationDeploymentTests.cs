using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Pipelines;
using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using IngaCookBook.AppHost.Deployment;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.AspireIntegrationTests;

// Tests cover the custom pipeline built with Aspire's pinned preview pipeline API.
#pragma warning disable ASPIREPIPELINES001
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class MigrationDeploymentTests
{
    [Theory]
    [InlineData("Running")]
    [InlineData("Pending")]
    [InlineData("Processing")]
    public async Task MigrationWaitsForTheStartedExecutionToSucceedAsync(string initialStatus)
    {
        var job = CreateJob(initialStatus, "Succeeded");
        var clock = new FakeTimeProvider();
        var run = MigrationJobRunner.RunAsync(job, clock, TestContext.Current.CancellationToken);
        run.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(5));
        await run;

        await job.Received(1).StartAsync(WaitUntil.Completed, cancellationToken: Arg.Any<CancellationToken>());
        await job.Received(2).GetContainerAppJobExecutionAsync("this-release", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Stopped")]
    [InlineData("Degraded")]
    [InlineData("Unknown")]
    [InlineData("Unexpected")]
    [InlineData(null)]
    public async Task MigrationRejectsEveryUnsuccessfulTerminalStatusAsync(string? status)
    {
        var job = CreateJob(status);
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            MigrationJobRunner.RunAsync(job, new FakeTimeProvider(), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("this-release");
        exception.Message.ShouldContain("Deployment stopped");
        await job.Received(1).GetContainerAppJobExecutionAsync("this-release", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MigrationDeadlineStopsReleaseWithoutRestartingTheJobAsync()
    {
        var job = CreateJob("Running");
        var clock = new FakeTimeProvider();
        var run = MigrationJobRunner.RunAsync(job, clock, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(12));

        (await Should.ThrowAsync<TimeoutException>(() => run)).Message.ShouldContain("may still be running");
        await job.Received(1).StartAsync(WaitUntil.Completed, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MigrationPreservesCallerCancellationAsync()
    {
        var job = CreateJob("Running");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = MigrationJobRunner.RunAsync(job, new FakeTimeProvider(), cancellation.Token);
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => run);
        await job.Received(1).StartAsync(WaitUntil.Completed, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MigrationPreservesAzureStartFailureAndDoesNotPollAsync()
    {
        var job = CreateJob("Succeeded");
        var failure = new RequestFailedException(403, "Denied");
        job.StartAsync(WaitUntil.Completed, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ArmOperation<ContainerAppJobExecutionBase>>(failure));

        (await Should.ThrowAsync<RequestFailedException>(() =>
            MigrationJobRunner.RunAsync(job, new FakeTimeProvider(), TestContext.Current.CancellationToken)))
            .ShouldBeSameAs(failure);
        await job.DidNotReceiveWithAnyArgs().GetContainerAppJobExecutionAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MissingExecutionIsRetriedWithoutStartingAnotherJobAsync()
    {
        var job = CreateJob("Succeeded");
        var success = await job.GetContainerAppJobExecutionAsync("this-release", TestContext.Current.CancellationToken);
        job.ClearReceivedCalls();
        job.GetContainerAppJobExecutionAsync("this-release", Arg.Any<CancellationToken>()).Returns(
            Task.FromException<Response<ContainerAppJobExecutionResource>>(new RequestFailedException(404, "Not yet visible")),
            Task.FromResult(success));
        var clock = new FakeTimeProvider();
        var run = MigrationJobRunner.RunAsync(job, clock, TestContext.Current.CancellationToken);
        run.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(5));
        await run;

        await job.Received(1).StartAsync(WaitUntil.Completed, cancellationToken: Arg.Any<CancellationToken>());
        await job.Received(2).GetContainerAppJobExecutionAsync("this-release", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void MigrationSuccessGatesActualWebProvisioning()
    {
        var run = Step("run");
        var job = Step("job");
        var web = Step("web");
        MigrationDeployment.OrderSteps(run, [job], [web]);

        run.DependsOnSteps.ShouldBe(["job"]);
        web.DependsOnSteps.ShouldBe(["run"]);
        job.DependsOnSteps.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingProvisioningStepsFailClosed(bool missingJob)
    {
        Should.Throw<InvalidOperationException>(() => MigrationDeployment.OrderSteps(Step("run"),
            missingJob ? [] : [Step("job")], missingJob ? [Step("web")] : []));
    }

    private static PipelineStep Step(string name) => new() { Name = name, Action = _ => Task.CompletedTask };

    private static ContainerAppJobResource CreateJob(params string?[] states)
    {
        var job = Substitute.For<ContainerAppJobResource>();
        var operation = Substitute.For<ArmOperation<ContainerAppJobExecutionBase>>();
        operation.Value.Returns(ArmAppContainersModelFactory.ContainerAppJobExecutionBase(name: "this-release"));
        job.StartAsync(WaitUntil.Completed, cancellationToken: Arg.Any<CancellationToken>()).Returns(operation);
        var index = 0;
        job.GetContainerAppJobExecutionAsync("this-release", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var state = states[Math.Min(index++, states.Length - 1)];
            var resource = Substitute.For<ContainerAppJobExecutionResource>();
            resource.Data.Returns(ArmAppContainersModelFactory.ContainerAppJobExecutionData(
                id: null!, name: "this-release", resourceType: default, systemData: null!,
                status: state is null ? (JobExecutionRunningState?)null : new JobExecutionRunningState(state),
                startOn: null, endOn: null, template: null!, reason: null!));
            return Response.FromValue(resource, Substitute.For<Response>());
        });
        return job;
    }
}
