# Contracts/API agent instructions

Read domain model, API semantics, symbols/signs and `../contracts/openapi.json`. Own DTOs, command union, adapter mapping, examples and compatibility policy. Business logic stays in application/core.

Implement project isolation, strong revision preconditions, durable operation replay and RFC 9457 errors. Do not accept arbitrary file paths or raw model patches. Unknown quantities must validate only under the unknown variant; impossible numeric/unit combinations fail.

Test stale/replayed/different-body commands, unauthorized release, project-crossing IDs, job cancellation races and unknown command types. Add a real pinned OpenAPI/JSON Schema validator in application CI; package structural checks alone are not conformance proof. Change consumers/examples/migrations with the contract.
