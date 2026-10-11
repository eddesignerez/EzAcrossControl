# Atualização pelo GitHub

Windows e Android consultam a última release estável do repositório público
`eddesignerez/EzAcrossControl` ao abrir. Não usam token nem dependem do outro
aplicativo para consultar. Sem internet, a consulta falha sem bloquear o controle LAN.
Somente esta distribuição de software usa internet; mensagens e controle continuam locais.

O usuário escolhe **Atualizar agora** ou **Depois**. Não há download ou instalação
sem aceite. O pacote precisa ter nome, versão e endereço exatos do repositório,
tamanho limitado e SHA-256 fornecido pelo GitHub. Versões iguais, antigas,
rascunhos e pré-releases não são oferecidas. Hash verifica integridade, não é uma
assinatura independente do proprietário do repositório: a conta GitHub continua
sendo parte da cadeia de confiança.

## Windows

Depois do download validado, inicia o Inno Setup. O Windows solicita elevação;
cancelar essa autorização deixa o aplicativo aberto. Após iniciar o instalador,
o controle é liberado, o ADB é reiniciado e o Host encerra para liberar seus arquivos.
O aviso de aceite informa que outras conexões Android podem ser interrompidas.
O instalador preserva as configurações existentes e reabre o aplicativo ao concluir
uma atualização iniciada com `/EZACUPDATE`. Não aceita scripts ou comandos da release.

## Android

O DownloadManager acompanha somente o download aprovado, com versão, hash e
identificador persistidos. Após permitir instalação de aplicativos desta origem,
o download começa ao retornar. Ao concluir no primeiro plano, valida SHA-256,
packageName, versionName, versionCode maior e os mesmos assinantes do APK instalado.
O instalador recebe uma cópia privada validada, exposta por FileProvider não exportado
e limitado à pasta de atualizações. Em segundo plano, continua ao voltar ao aplicativo.

A confirmação final pertence ao Android. A instalação não é silenciosa. Cancelar
o instalador não o reabre em loop. Cancelar o download revoga o aceite pendente.

## Referências e decisões de interface

Fluxo autorizado pelo usuário após inspeção do atualizador Android do EzDeck:
aceite, permissão do sistema, download persistente e abertura do instalador.
Não foi copiado o projeto nem introduzida uma dependência entre aplicativos.
Alertas usam Inter e os tokens/tema existentes; primário instala, secundário adia/cancela.
Progresso e falhas ficam no mesmo aviso. Refero online indisponível nesta conta;
foram usados o fluxo fornecido e as diretrizes locais de foco, feedback e acessibilidade.

- GitHub Releases: https://docs.github.com/en/rest/releases/releases#get-the-latest-release
- Android: https://developer.android.com/reference/android/content/pm/PackageInstaller.SessionParams#setRequireUserAction(int)

Validação real de upgrade exige uma release posterior publicada, consentimento
e confirmação do sistema; testes sintéticos não equivalem a instalação real.

## Validação da implementação 1.1.19

- 69 testes Windows e 30 testes Android aprovados. Incluem comparação de versões,
  origem e hash, download cancelado/incompleto, redirecionamento do CDN oficial,
  aceite e retorno da permissão Android, cancelamento e identidade/assinantes do APK.
- Aviso Windows renderizado nos temas claro/escuro, português/inglês e estados
  oferta/download/falha, sem iniciar o Host nem alterar preferências persistidas.
- Consulta real pelo novo código Windows reconheceu a release pública 1.1.18
  para uma versão anterior e não ofereceu downgrade para 1.1.19.
- APK release 1.1.19, versionCode 17, assinatura verificada e igual à de 1.1.18.
  Instalador Inno Setup e pacote portátil gerados localmente.
- O APK 1.1.19 foi instalado no HiPadPlus USB e a versão 1.1.19 foi confirmada.
  A instalação e reabertura automáticas de uma atualização iniciada pelo aviso
  ainda dependem de testar uma release posterior publicada.
