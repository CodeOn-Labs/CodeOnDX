using Dx.IntegrationTests.Infrastructure;
using Xunit.Sdk;
using Xunit.v3;

// One database + API for the whole run; tests run one at a time because some of them change
// shared rows (tenant rules, lockout) and restore them afterwards.
[assembly: AssemblyFixture(typeof(ApiFixture))]
[assembly: Parallelization(Mode = ParallelMode.None)]
