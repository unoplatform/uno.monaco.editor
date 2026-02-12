# fn-9-fixdesktop-jsonrpc-startlistening.1 Move StartListening after all AddLocalRpcTarget calls in CreateBridgeTargets

## Description
TBD

## Acceptance
- [ ] TBD

## Done summary
Moved `_jsonRpc.StartListening()` from `SetupJsonRpc()` to after all `AddLocalRpcTarget()` calls in `CreateBridgeTargets()`. StreamJsonRpc locks its configuration once listening begins, so targets must be registered first. Build passes with 0 errors.
## Evidence
- Commits:
- Tests:
- PRs: