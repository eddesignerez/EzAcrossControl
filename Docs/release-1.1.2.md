# EZ Across Control 1.1.2

O servidor scrcpy enviado pelo Windows agora apresenta o teclado UHID do EZ Across ao Android como teclado externo. O mouse UHID mantém a identificação original. O APK usa o mesmo código funcional da versão 1.1.0, com número de versão atualizado para permitir uma atualização normal.

## Validação

- O HiPadPlus, depois de restaurado às configurações de fábrica, mostrou somente a barra do Gboard com o teclado do Windows; o usuário confirmou que a troca pela borda e os controles ficaram funcionais.
- No LAVIE T11, o usuário confirmou a barra reduzida, cliques e digitação funcionando.
- O servidor de distribuição é o mesmo binário testado no HiPadPlus. O patch correspondente e o procedimento de compilação acompanham as fontes nativas.
- A configuração “Mostrar teclado virtual com teclado físico” foi verificada desligada no HiPadPlus. Bluetooth também mostrou a barra reduzida nesse aparelho.

## Limites

- A simples mudança para teclado externo não resolveu o Gboard no HiPadPlus antes da restauração; a causa daquele estado anterior do Android não foi identificada. Não é necessário presumir que uma restauração de fábrica seja exigida em outros aparelhos.
- Continuam necessários depuração USB autorizada ou depuração sem fio pareada, além da acessibilidade do APK para o retorno pela borda.
- O instalador Windows não possui assinatura Authenticode. O APK usa a assinatura de distribuição existente.

O pacote inclui instalador e ZIP portátil para Windows, APK Android, fontes e manifesto das dependências nativas e verificações SHA-256.
