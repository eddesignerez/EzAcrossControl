# EZ Across Control 1.1.23

## Correção

- Uma sondagem transitória de prontidão ADB não rebaixa mais uma sessão de controle
  que o motor Windows já confirmou como `Ready` ou `Captured`.
- Windows e Android mantêm o estado remoto baseado no motor de controle ativo e no
  transporte realmente selecionado; a sondagem continua sendo usada antes de iniciar.
- Android `versionCode` incrementado para `21` para permitir atualização sobre a 1.1.22.

## Atualização automática

- Esta release estável serve como teste da consulta ao GitHub ao iniciar o Windows e o APK.
- No Windows, aceitar deve baixar o instalador e iniciá-lo; no Android, aceitar deve baixar
  o APK e abrir o instalador do sistema após a validação e as permissões necessárias.
- A instalação ainda depende da confirmação do usuário nos diálogos do Windows/Android.

## Validação

- Suíte Windows: 69 testes aprovados.
- Suíte unitária Android: aprovada.
- Validar o aviso e o fluxo final de instalação requer abrir a versão 1.1.22 e aceitar
  a atualização para 1.1.23 em cada plataforma.
