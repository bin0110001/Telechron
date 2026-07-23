using Xunit;

// The live tests in this assembly share one external, stateful resource
// (the local Podman machine's container list) and assert on its exact
// before/after state -- running test classes in parallel makes those
// diffs unreliable, since a container created by one class is visible to
// another class's "list all containers" snapshot.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
