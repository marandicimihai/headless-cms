using FastEndpoints.Testing;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HeadlessCms.Api.Tests;

public sealed class TestAppCollection : TestCollection<TestApp>;
