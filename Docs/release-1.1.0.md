# Release 1.1.0

## Implementado

- Instalador Windows x64 com runtime .NET e dependências nativas completas.
- APK de distribuição assinado, versão 1.1.0 / código 2.
- 11 idiomas, escolha persistente e idioma do sistema no Android.
- Ícone Android com símbolo maior e fundo semântico azul.
- Preferências Windows salvas em LocalAppData, compatíveis com instalação em Program Files.
- Bandeja, temas, painel avançado e diagnóstico de disponibilidade USB/Wi-Fi.
- Porta padrão 8765, configurável na interface.

## Evidência

- 27 testes Windows aprovados.
- 17 testes Android aprovados, incluindo disponibilidade de transporte, estado de controle e resolução de idiomas.
- Instalação Windows concluída; scrcpy executado com PATH contendo somente System32.
- Assinatura do APK verificada e release sem atributo debuggable.
- Seletor Android exercitado no HiPadPlus, com troca imediata para inglês.
- Layout Windows exercitado em pt-BR e francês; janela e botões se ajustam ao tamanho dos textos.
- Controle físico e retorno pela borda exercitados nas fases anteriores no HiPadPlus.

## Limites

- O instalador Windows não possui assinatura Authenticode.
- Atualização de uma instalação Android com assinatura de depuração exige desinstalar a versão anterior.
- Outros aparelhos/fabricantes não foram fisicamente validados.
- Configurações de depuração e acessibilidade continuam sendo habilitadas pelo usuário no menu do Android.
