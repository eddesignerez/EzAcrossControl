# EzAcrossControl

Controle de dispositivos Android a partir de um computador Windows, desenvolvido como um projeto independente da ElementZero.

## Resumo

O aplicativo Windows usa WPF (.NET 8) para gerenciar a conexão, a captura de mouse e teclado e a troca de controle nas bordas da tela. O aplicativo Android fornece a interface de conexão e os serviços auxiliares. A comunicação do aplicativo entre Windows e Android usa WebSocket e mensagens JSON documentadas em [`Protocol/protocol.md`](Protocol/protocol.md), com foco em uso na rede local e baixa latência.

O motor de controle V2 integra uma versão adaptada do scrcpy para entrada nativa via UHID. Há modos de transporte USB, Wi-Fi e automático no código atual. O patch e o procedimento de compilação ficam em [`patches/scrcpy`](patches/scrcpy) e [`Docs/v2/build-scrcpy-windows.md`](Docs/v2/build-scrcpy-windows.md). Binários gerados, SDKs locais e cópias de terceiros não fazem parte deste repositório.

## Estado atual

- Host Windows, cliente Android, protocolo JSON e design system estão presentes.
- Captura de entrada, seleção de dispositivo e transição de borda têm implementação e testes locais.
- O fluxo V2 com scrcpy e UHID já foi exercitado em USB e Wi-Fi; a aceitação física de suavidade do ponteiro, cliques e retorno em diferentes dispositivos ainda depende de validação manual.
- O projeto segue em desenvolvimento. Esta publicação reúne o código fonte e a documentação existentes; não é uma versão de distribuição pronta para instalar.

Consulte [`Docs/status.md`](Docs/status.md) para o histórico de fases e [`Docs/architecture.md`](Docs/architecture.md) para os componentes. O protocolo entre os aplicativos está em [`Protocol/protocol.md`](Protocol/protocol.md).

## Estrutura

| Pasta | Conteúdo |
| --- | --- |
| `Windows-host/` | Aplicativo WPF, captura de entrada e integração com o motor V2 |
| `Android-client/` | Aplicativo Android em Kotlin/Compose |
| `Android-server/` | Código auxiliar para Android |
| `Protocol/` | Contrato de mensagens JSON |
| `Tests/` | Testes do host e do protocolo |
| `Tools/` | Ferramentas locais de diagnóstico |
| `patches/` e `scripts/` | Patch e scripts para compilar o motor adaptado |
| `Docs/` | Arquitetura, estado e notas de implementação |

## Desenvolvimento

Requisitos principais: Windows, .NET 8 SDK e Android SDK com JDK 17. O motor V2 precisa ser compilado separadamente conforme a documentação; não execute o script de compilação sem revisar seus passos de instalação de dependências e atualização da cópia local do scrcpy.

```powershell
dotnet build .\Windows-host\WindowsHost.csproj
dotnet test .\Tests\WindowsHost.Tests\WindowsHost.Tests.csproj
cd .\Android-client
.\gradlew.bat assembleDebug
```

Configurações locais, endereços de rede e credenciais devem permanecer fora do Git. A porta padrão do protocolo é `8765` e deve ser configurada pelos pontos centrais de configuração do host e do cliente.
