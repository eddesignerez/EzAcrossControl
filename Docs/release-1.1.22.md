# EZ Across Control 1.1.22

## Correções e melhorias

- O Windows e o Android distinguem o app autenticado do controle remoto realmente pronto.
- O estado pronto agora depende do ADB do transporte selecionado, do motor do Windows,
  do serviço de acessibilidade Android e do controle não estar pausado.
- O APK envia a situação atual do ADB ao Windows; a perda do ADB Wi-Fi não fica mascarada
  por uma conexão WebSocket ainda aberta.
- O painel Windows mostra a etapa atual da conexão e não marca o ícone como conectado
  enquanto a cadeia de controle não estiver operacional.
- No APK, os botões de configuração de depuração e acessibilidade ficam mais legíveis;
  os estados do teclado remoto são “Sim/Não” e as ações aparecem empilhadas.
- A versão Android passa a `1.1.22` (versionCode `20`); Windows também passa a `1.1.22`.

## Atualizações

Windows e Android verificam a release estável do GitHub ao abrir e avisam se há versão
mais recente. A instalação só começa após aceite e segue as confirmações do sistema.

## Validação

- Build de release Windows e APK Android concluídos.
- Testes unitários Android aprovados.
- Assinatura do APK de release comparada com a assinatura publicada da versão `1.1.21`.
- O teste final de atualização requer aceite do usuário e execução em Windows/Android.
