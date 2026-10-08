# Build Custom Scrcpy on Windows

To ensure 100% parity with official UHID input behavior while allowing EZ Across to control the capture states via Named Pipes, we build a custom `scrcpy` executable from the official source.

## Requisitos

- Windows 10/11
- Conexão com a internet (para baixar dependências e o código fonte).
- O script automatizará a instalação do ambiente **MSYS2** e todas as ferramentas de build (Meson, Ninja, GCC).

## Como reproduzir o Build

1. Abra o PowerShell como Administrador (necessário para a instalação do MSYS2 via winget).
2. Navegue até a raiz do projeto `EZ Across Control`.
3. Execute o script de automação:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\build-scrcpy-ezacross.ps1
   ```
4. Se solicitado pelo Windows (UAC), confirme a instalação do MSYS2.
5. O script irá:
   - Instalar e atualizar o MSYS2
   - Instalar dependências de C via pacman (SDL3, FFmpeg, etc)
   - Clonar o scrcpy (`v4.1`)
   - Aplicar o patch IPC localizado em `patches/scrcpy/EZ_ACROSS_PATCH.patch`
   - Baixar o `scrcpy-server-v4.1` oficial pré-compilado e validar o SHA-256
   - Executar o Meson e o Ninja
   - Copiar os artefatos compilados para `third_party/scrcpy-ezacross/bin/`

## Output (Resultado)

Os arquivos gerados ficarão isolados em:
`third_party/scrcpy-ezacross/bin/`

O arquivo principal é o `scrcpy.exe`. 
O `ScrcpyProcessManager.cs` no aplicativo WPF apontará automaticamente para este binário.

## Como Atualizar Futuramente

Se você quiser atualizar a versão do scrcpy no futuro:
1. Edite `scripts/build-scrcpy-ezacross.ps1` e modifique a tag/commit (`v4.1`) e a URL do prebuilt server (`scrcpy-server-v4.X`).
2. Atualize o `ExpectedServerHash` no script para corresponder ao novo release oficial.
3. Certifique-se de que o patch `EZ_ACROSS_PATCH.patch` continua compatível (use `git apply --check`).
4. Rode o script de build novamente.

Para limpar os arquivos temporários de compilação sem afetar os binários finais, rode:
```powershell
.\scripts\clean-scrcpy-build.ps1
```
