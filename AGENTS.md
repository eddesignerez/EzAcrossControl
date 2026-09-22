# Core Rules for EZ Across Control Agents

1. **LAN as Main Transport**: All communication between Windows and Android must occur over the Local Area Network (LAN).
2. **No Cloud**: Do not implement any cloud-based services for routing or messaging.
3. **Low Latency**: Prioritize performance and low latency in all network implementations.
4. **Decoupled Architecture**: Windows and Android components must remain decoupled, relying strictly on the JSON protocol defined in `Protocol/protocol.md`.
5. **No Secrets in Git**: Never commit hardcoded IPs, passwords, or any sensitive information. Use `.gitignore` properly.
6. **Network Configuration**: Use port 8765 as the default. Ports 3000, 4000, 5000, and 5173 are reserved and must NOT be used. Port should not be hardcoded, but centrally configurable.
7. **Design System**: Strict adherence to the Design System tokens. Do not use hardcoded HEX values directly in views/components. Use semantic tokens (SuccessPrimary, Background, etc.). Font must be Inter.
