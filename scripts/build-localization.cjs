const fs = require('fs');
const languages = [{code:'pt-BR',label:'Português (Brasil)'},{code:'en',label:'English'},{code:'es',label:'Español'},{code:'ja',label:'日本語'},{code:'it',label:'Italiano'},{code:'fr',label:'Français'},{code:'de',label:'Deutsch'},{code:'zh-CN',label:'简体中文'},{code:'vi',label:'Tiếng Việt'},{code:'ko',label:'한국어'},{code:'ar',label:'العربية'}];
const columns = ['en','pt-BR','es','ja','it','fr','de','zh-CN','vi','ko','ar'];
const translations = Object.fromEntries(columns.map(c=>[c,{}]));
for (const line of fs.readFileSync('Localization/messages.tsv','utf8').trim().split(/\r?\n/)) {
  const row=line.split('|'); if(row.length!==11 || row.some(v=>!v)) throw Error('Incomplete translation: '+row[0]);
  if(translations.en[row[0]]) throw Error('Duplicate key: '+row[0]);
  columns.forEach((code,i)=>translations[code][row[0]]=row[i]);
}
Object.assign(translations.en, {
 'Update Offer':'Version {0} is available. Accept to download and start installation. Windows will request permission. ADB will restart and may briefly interrupt other Android connections.',
 'Update Android Offer':'Version {0} is available. Accept to download and open the installer automatically. Confirm installation on Android.',
 'Update Downloading':'Downloading update: {0}%', 'Update Validating':'Verifying package…',
 'Update Failed':'Could not update. Check your internet connection and try again.',
 'Update Invalid Package':'Package verification failed. No installation was started.',
 'Update Permission Needed':'Allow this app to install updates. Download continues when you return.',
 'Update Permission Denied':'Installation was not authorized. Choose Update Now to try again.',
 'CONTROLE ENTRE TELAS':'CONTROL BETWEEN SCREENS','Conectar':'Connect','Conectado':'Connected','Aguardando':'Waiting','Desconectado':'Disconnected','Desconectar':'Disconnect',
 'TOQUE AQUI':'TAP HERE','TOQUE PARA SAIR':'TAP TO DISCONNECT','Endereço do IP':'IP Address','Porta':'Port','Modo Avançado':'Advanced Mode','Depuração':'Debugging','Opções do Desenvolvedor':'Developer Options',
 'Auto: preferência USB; Wi-Fi quando necessário.':'Auto: prefers USB; uses Wi-Fi when needed.','Aguardando Host':'Waiting for Host',
 'Controle Indisponível. Verifique a Depuração.':'Control Unavailable. Check Debugging.','Controle desconectado. Verifique a depuração.':'Control disconnected. Check debugging.',
 'Pronto para Conectar via USB':'Ready to Connect via USB','Pronto para Conectar via Wi-Fi':'Ready to Connect via Wi-Fi',
 'Ative as Opções do Desenvolvedor':'Enable Developer Options','Ative a Depuração USB':'Enable USB Debugging','Ative a Depuração Wi-Fi':'Enable Wi-Fi Debugging','Ative a Depuração Wi-Fi ou USB':'Enable Wi-Fi or USB Debugging',
 'Abra o Host PC e verifique a conexão LAN':'Open the Host PC and check the LAN connection','Conecte o cabo USB e autorize a depuração':'Connect the USB cable and authorize debugging',
 'Conecte e autorize a Depuração Wi-Fi no Host PC':'Connect and authorize Wi-Fi debugging on the Host PC','Conecte e autorize a Depuração Wi-Fi ou USB':'Connect and authorize Wi-Fi or USB debugging',
 'Endereço do IP inválido':'Invalid IP Address','Porta inválida':'Invalid Port','Host não respondeu':'Host did not respond','Falha na conexão com Host':'Connection to Host failed','Conexão com Host encerrada':'Connection to Host closed','Protocolo do Host incompatível':'Incompatible Host protocol'
});
fs.writeFileSync('Localization/catalog.json',JSON.stringify({languages,translations},null,2)+'\n');
console.log(`${languages.length} languages, ${Object.keys(translations.en).length} messages per language`);
