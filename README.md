# EZ Across Control

Controle seu Android com o mouse e o teclado do Windows e alterne entre as telas pela borda do monitor. A comunicação entre os aplicativos fica na rede local, sem conta e sem serviços de nuvem.

## Baixar e instalar

**Windows 1.1.1 e Android 1.1.0 — versões funcionais.**

- [Instalador Windows (.exe)](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.1/EZAcrossControl-1.1.1-windows-x64-setup.exe)
- [Aplicativo Android (.apk)](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.0/EZAcrossControl-1.1.0-android.apk)
- [Windows portátil (.zip)](https://github.com/eddesignerez/EzAcrossControl/releases/download/v1.1.1/EZAcrossControl-1.1.1-windows-x64-portable.zip)
- [Versões, fontes de terceiros e verificações de integridade](https://github.com/eddesignerez/EzAcrossControl/releases)

### Requisitos

- Windows 10/11 de 64 bits. O instalador inclui .NET, ADB e o motor de controle; não é necessário instalar um SDK.
- APK: Android 7 ou posterior. O controle nativo depende do suporte UHID do aparelho; algumas implementações de fabricante podem exigir configurações adicionais.
- Windows e Android na mesma rede local, inclusive quando USB é escolhido para o motor de controle.
- Depuração USB autorizada ou depuração sem fio ativada e pareada. A depuração sem fio pelo menu do Android requer Android 11 ou posterior.

### Primeiro uso

1. Execute o instalador Windows e abra **EZ Across Control**. A instalação solicita permissão de administrador para configurar o servidor local e o firewall da rede privada.
2. Instale o APK no Android. Permita a instalação pelo aplicativo usado para abrir o arquivo, caso o sistema solicite.
3. Em **Modo Avançado → Depuração → Abrir Configurações**, ative as opções de desenvolvedor e o método de depuração desejado. Autorize o computador quando o Android perguntar.
4. Para Wi-Fi, abra **Depuração sem fio → Parear dispositivo com código de pareamento**. No computador, execute o ADB incluído no aplicativo:

   ```powershell
   & "$env:ProgramFiles\EZ Across Control\scrcpy\adb.exe" pair <IP-do-Android>:<porta-de-pareamento>
   ```

   Digite o código exibido pelo Android. A porta de pareamento e a porta de conexão são diferentes. Se a descoberta automática não encontrar o aparelho, use `adb connect <IP-do-Android>:<porta-de-conexão>`.

5. No APK, escolha **Modo de Conexão**: **Auto**, **Wi-Fi** ou **USB**. Auto prefere Wi-Fi e usa USB quando disponível.
6. Em **Host PC**, informe o IP exibido no Windows e a mesma porta nos dois aplicativos. A porta padrão é **8765**.
7. Toque no círculo **Conectar**. O aplicativo informa quando o controle está disponível por Wi-Fi ou USB. Escolha a posição do Android no Windows e mova o ponteiro até essa borda para alternar o controle.

### Controle, teclado e configurações

- **Acessibilidade** auxilia o controle remoto e o retorno pela borda. A ativação é feita pelo usuário no menu do Android.
- **Teclado Remoto** informa se o teclado auxiliar está ativado e selecionado. O motor nativo também usa um teclado HID; configure seu idioma/layout nas opções de teclado físico do Android quando necessário.
- **Parar Controle** libera o controle remoto e encerra a conexão do APK. Toque novamente em Conectar para iniciar outra sessão.
- O botão **X** no Windows envia o aplicativo à bandeja. Use **Restaurar** para reabrir e **Sair** para encerrá-lo.
- Os ícones de sol/lua alternam os temas. Diagnóstico e registro de atividade ficam no modo avançado.

## Idiomas

Português, English, Español, 日本語, Italiano, Français, Deutsch, 简体中文, Tiếng Việt, 한국어 e العربية.

O Android segue o idioma do sistema por padrão. Para alterar, abra **Modo Avançado → Idioma**, abaixo da depuração. No Windows, o seletor fica na seção de diagnóstico. A escolha é salva localmente.

## Solução de problemas

- **Depuração desligada ou dispositivo não autorizado:** abra as configurações indicadas pelo APK, habilite a opção e autorize o computador.
- **Wi-Fi caiu:** confira a depuração sem fio e a porta atual; o Android pode mudar a porta depois de desligar/ligar essa opção. O aplicativo atualiza a disponibilidade do controle enquanto está aberto.
- **Servidor inacessível:** confirme o IP, a porta e que a rede Windows está marcada como privada. A regra criada pelo instalador permite apenas a rede local privada.
- **Porta diferente de 8765 (Windows 1.1.1):** pare o servidor, altere a porta e inicie novamente. O aplicativo solicita autorização do Windows se precisar ajustar a reserva de URL ou a regra de firewall. A porta é salva após o início bem-sucedido; configure o mesmo valor no Android. A regra permite apenas TCP na porta selecionada, em redes privadas e na sub-rede local. Na versão 1.1.0, portas personalizadas precisam de configuração manual.
- **APK de testes anterior:** a versão de distribuição usa uma assinatura própria. Para migrar de um APK de depuração, desinstale a versão antiga e instale esta. As preferências deverão ser informadas novamente.
- **Windows portátil:** execute como administrador para escutar na LAN ou configure a reserva de URL/firewall. A versão instalável configura a porta padrão.

Os testes físicos foram realizados em um HiPadPlus. A compatibilidade com outros fabricantes e a sensação de movimento devem ser verificadas no aparelho usado. O instalador Windows ainda não tem assinatura Authenticode; o APK possui assinatura de distribuição.

## Arquitetura e desenvolvimento

O host WPF (.NET 8) e o cliente Kotlin/Compose usam o contrato JSON em [Protocol/protocol.md](Protocol/protocol.md). O motor nativo integra scrcpy com um patch para captura e retorno de entrada. Não há roteamento pela nuvem.

| Pasta | Conteúdo |
| --- | --- |
| `Windows-host/` | Host Windows, bandeja, captura e motor de controle |
| `Android-client/` | APK, conexão circular, acessibilidade e teclado auxiliar |
| `Localization/` | Catálogo compartilhado dos 11 idiomas |
| `Protocol/` | Contrato JSON entre os aplicativos |
| `Tests/` | Testes do host |
| `packaging/` | Instalador Windows |
| `scripts/` | Geração de idiomas, compilações e fontes de distribuição |
| `patches/` | Alterações no scrcpy |

Requisitos de desenvolvimento: .NET 8 SDK, Android SDK, JDK 17 e Node.js para gerar o catálogo de idiomas. Para compilar o motor, consulte [Docs/v2/build-scrcpy-windows.md](Docs/v2/build-scrcpy-windows.md).

```powershell
node scripts/build-localization.cjs
 dotnet build Windows-host/WindowsHost.csproj
 dotnet test Tests/WindowsHost.Tests/WindowsHost.Tests.csproj -c Release
cd Android-client
.\gradlew.bat :app:assembleDebug :app:testDebugUnitTest
```

Os scripts `build-windows-release.ps1` e `build-android-release.ps1` geram os pacotes de distribuição. O APK de release precisa de uma chave própria; o script mantém a chave e a senha na pasta local ignorada `.release-private`. Guarde essa pasta com segurança para assinar futuras atualizações. Nunca envie chaves, preferências pessoais ou logs ao GitHub.

As traduções ficam em `Localization/messages.tsv`; execute o gerador depois de alterá-las. O catálogo é consumido pelas duas plataformas sem alterar os valores do protocolo.

## Terceiros

Consulte [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), as licenças incluídas na instalação e o arquivo de fontes nativas da release. Esses componentes mantêm suas próprias licenças.
