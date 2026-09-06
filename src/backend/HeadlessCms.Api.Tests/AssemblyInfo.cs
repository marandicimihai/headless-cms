using FastEndpoints.Testing;
using Xunit;

namespace HeadlessCms.Api.Tests;

public sealed class TestAppCollection : TestCollection<TestApp>;
public sealed class WorkspaceEndpointCollection : TestCollection<WorkspaceEndpointTestApp>;
public sealed class WorkspaceMembershipCollection : TestCollection<WorkspaceMembershipTestApp>;
public sealed class WorkspaceAdministrationCollection : TestCollection<WorkspaceAdministrationTestApp>;
public sealed class ProjectEndpointCollection : TestCollection<ProjectEndpointTestApp>;
public sealed class ContentEndpointCollection : TestCollection<ContentEndpointTestApp>;
public sealed class RemainingEndpointCollection : TestCollection<RemainingEndpointTestApp>;
