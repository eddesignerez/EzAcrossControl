# Phase 3.0: UHID Feasibility Prototype

## 1. Arquitetura Alvo (UHID Backend)

O objetivo é abandonar a simulação de alto nível (Accessibility, Cursor Overlay, IME customizado) em favor da injeção de hardware virtual em nível de kernel usando o subsistema **UHID (User-space HID)** do Linux/Android.

**Fluxo de Dados:**
1. **Windows Local:** Captura eventos de mouse e teclado via Raw Input (já implementado).
2. **Network (LAN/Wi-Fi):** Envia os deltas de mouse (relative X, relative Y, botões, wheel) e estados de teclado (scancodes HID).
3. **Android Shell Server:** Um executável Java rodando no Android (via `app_process`) recebe os pacotes, formata os structs `uhid_event` e escreve diretamente no character device `/dev/uhid`.
4. **Android OS:** O kernel Linux interpreta os dados como se um teclado e mouse físicos tivessem sido plugados via USB ou Bluetooth. O próprio sistema operacional renderiza o cursor do mouse e lida com atalhos de teclado (incluindo IMEs físicos).

## 2. Privilégios e Modelo de Execução

Um APK Android comum não possui permissões para abrir `/dev/uhid`. O SELinux e as permissões de arquivo do Android bloqueiam o acesso a esse device node para aplicativos de usuário (`u:r:untrusted_app:s0`).

**Como o scrcpy resolve isso (e como resolveremos):**
O `scrcpy` faz o deploy de um `.jar` (server) no dispositivo e o executa utilizando o comando `app_process` através do **ADB shell**.
O usuário do shell ADB (`uid=2000(shell) gid=2000(shell)`) possui os grupos necessários (frequentemente `uhid` ou `input`) e as políticas SELinux permissivas (`u:r:shell:s0`) que permitem abrir `/dev/uhid` com `O_RDWR`.

**Root NÃO é necessário.** A execução via ADB shell é suficiente.
*Se o tablet atual possuir uma customização de fabricante que bloqueia o usuário `shell` de acessar `/dev/uhid`, este será um bloqueador (NO-GO).*

## 3. ADB e Wireless Debugging (UX)

Como dependemos do ADB shell, o fluxo de inicialização muda. A porta TCP 8765 do nosso App não pode iniciar o servidor shell por conta própria.

**Setup Inicial (Primeira Vez):**
1. O usuário habilita "Developer Options" e "USB Debugging" ou "Wireless Debugging" no Android.
2. O EZ Across Host no Windows detecta o device (via ADB over USB ou pareamento Wireless).
3. O Host faz o push do `ezacross-server.jar` para `/data/local/tmp/`.
4. O Host executa o comando para iniciar o servidor shell em background.

**Uso Normal:**
Uma vez autorizado, e com o servidor shell rodando, a conexão é transparente via rede Wi-Fi/LAN. Se o dispositivo reiniciar, será necessário iniciar o servidor shell novamente (via Wireless Debugging, o EZ Across Host pode tentar reconectar automaticamente via TCP/IP se o IP for conhecido e a porta ADB estiver aberta).

## 4. Referência: scrcpy (Genymobile)

O repositório oficial [scrcpy](https://github.com/Genymobile/scrcpy) é a referência principal para esta implementação.
- **Arquivo Chave:** `server/src/main/java/com/genymobile/scrcpy/control/UhidManager.java`
- **Funcionamento:** O `UhidManager` abre `/dev/uhid` via `android.system.Os.open()`. Ele envia os structs `uhid_create2_req` para criar os dispositivos e `uhid_input2_req` para enviar os reports.
- **Licença:** Apache 2.0. Ao adaptarmos a mecânica de serialização dos structs C para Java `ByteBuffer` do scrcpy, devemos incluir os notices da licença Apache 2.0 e preservar o copyright da Genymobile.

## 5. Mouse HID Descriptor

O mouse virtual precisa de um descritor HID que o identifique como um mouse com suporte a movimento relativo e scroll.
Exemplo de descritor (similar ao do scrcpy):
- Usage Page (Generic Desktop)
- Usage (Mouse)
- Collection (Application)
  - Usage (Pointer)
  - Collection (Physical)
    - Buttons (1 to 5) (Left, Right, Middle, Forward, Back)
    - X, Y (Relative, 16-bit ou 8-bit)
    - Wheel (Vertical, Relative)
    - AC Pan (Horizontal Wheel, Relative)

Este descritor fará o Android renderizar o seu próprio cursor, sem depender do `CursorOverlayManager`.

## 6. Keyboard HID Descriptor

O teclado virtual deve ser reportado como um teclado padrão (Boot Keyboard).
Exemplo:
- Usage Page (Generic Desktop)
- Usage (Keyboard)
- Modifiers (8 bits: LCtrl, LShift, LAlt, LMeta, RCtrl, RShift, RAlt, RMeta)
- Reserved (1 byte)
- Key Array (6 bytes, permitindo 6 teclas simultâneas)

Isso enviará scancodes (ex: Usage ID `0x04` para 'A'). O Android receberá esses códigos e os processará pelo mapa de teclado físico selecionado nas configurações do sistema.

## 7. Layout Japonês e Teste do IME

Ao simular um teclado físico real, o Android tratará os inputs através de suas configurações de **Physical Keyboard** (Settings -> System -> Languages & input -> Physical keyboard).
- O usuário poderá escolher o layout Japonês apropriado para o teclado físico simulado.
- Teclas específicas como **Zenkaku/Hankaku (`0x85`)**, **Henkan (`0x8A`)**, e **Muhenkan (`0x8B`)** podem ser transmitidas nos reports UHID (se incluirmos suporte na Usage Page).
- O IME ativo (ex: Gboard) deverá reagir a essas teclas e abrir a janela de composição (candidatos) nativamente na tela do Android, lidando com toda a conversão hiragana/kanji sem precisarmos de uma ponte manual.

## 8. Handoff e Retorno pela Borda (Limitação)

**Desafio:** Com o cursor sendo gerenciado inteiramente pelo Android, nosso app/shell server não sabe nativamente a posição exata (X,Y) do cursor na tela a cada momento. Assim, detectar quando o cursor "bate na borda" do tablet para retornar ao Windows é complexo.

**Opções Iniciais para Retorno:**
1. **Emergency Exit:** Pressionar `ESC` ou um atalho específico (ex: `Ctrl+Alt+Backspace`) no Windows sinaliza o retorno imediato.
2. **Polling de Posição (Experimental):** Usar dumpsys ou APIs privadas (SurfaceFlinger/InputManager) para ler a posição do cursor (o que pode ser frágil e versão-dependente).
3. **Virtual Bounds:** Manter uma posição "estimada" no backend, somando os deltas enviados. Isso perde sincronia se o cursor bater em uma borda física, mas como controlamos o envio, podemos tentar resincronizar ocasionalmente.

Para a Fase 3.0, usaremos `ESC` (Emergency Return) como fallback garantido durante os testes.

## 9. Comparativo: UHID vs Accessibility Backend

| Recurso | Current Backend (Accessibility + IME) | New Backend (UHID) |
|---|---|---|
| **Mouse Cursor** | Falso (Overlay customizado), sujeito a latência de rendering no App. | Nativo do Android, acelerado por hardware, sem latência adicional. |
| **Mouse Clicks** | Injetados via Accessibility (limitado, sem drag/hold perfeito). | Físico real (Left/Right/Middle, Drag, Hold, Scroll nativos). |
| **Keyboard Input** | EZ Across IME customizado (`commitText`). Suporte falho a shortcuts complexos e idiomas. | Físico real. Suporta todos os atalhos (Alt+Tab, Ctrl+C), repeats, Gboard, e entrada Japonesa nativa. |
| **Privilégios** | Permissão de Acessibilidade (fácil via UI do app). | ADB Shell (`app_process`), exige setup inicial USB/Wireless Debugging. |
| **Clipboard** | Sincronização de texto possível pelo IME. | Requer um canal de controle paralelo (separado do UHID). |

## 10. Conclusão

O UHID representa o "Santo Graal" do controle remoto para Android, oferecendo uma experiência indistinguível de periféricos físicos. A dependência do ADB Shell é um pequeno custo de UX na instalação, amplamente superado pela qualidade do controle (especialmente o suporte a teclado japonês nativo e cursor por hardware).

**O próximo passo é realizar o Teste de Viabilidade (Walkthrough Go / No-Go) utilizando a ferramenta `scrcpy` oficial no tablet de destino.**
