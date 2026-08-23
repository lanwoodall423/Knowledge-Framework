# Knowledge Framework Runtime Validation

RimTest is the development entry point for readiness, affected-test selection,
build, deployment, artifact freshness, and in-game validation. Run `rimtest
doctor --json` before relying on validation and use `rimtest affected --run
--json` for current-source validation. RimTest delegates lifecycle, readiness,
generation identity, and test leases to DevBridge2; do not invoke lower-layer
lifecycle commands or launch/kill RimWorld as a substitute for that workflow.

The configured `knowledge-framework-development-smoke` recipe proves that the
current developer assembly is built, deployed, loaded, and reaches the
Quicktest map during an affected source run. It does not invoke
`KnowledgeFrameworkVerification.RunGameTests`; therefore it must not be
reported as passing the deleted automatic V2/V3 behavioral suite. The pure
behavioral harness and manual bounded stress action remain framework-owned, and
the framework remains usable without development tooling.
