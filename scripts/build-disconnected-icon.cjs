// Render the user's SVG without changing its paths or palette.
// Requires sharp from the workspace dependency bundle through NODE_PATH.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');

async function main() {
  const root = path.resolve(__dirname, '..');
  const assets = path.join(root, 'Windows-host', 'Assets');
  const source = path.join(assets, 'EzAcrossControlOFF.svg');
  await sharp(source).resize(256, 256).png().toFile(path.join(assets, 'EzAcrossControlOFF.png'));
  const sizes = [16, 24, 32, 48, 64, 128, 256];
  const images = await Promise.all(sizes.map(size => sharp(source).resize(size, size).png().toBuffer()));
  const header = Buffer.alloc(6 + sizes.length * 16);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(sizes.length, 4);
  let offset = header.length;
  images.forEach((image, i) => {
    const entry = 6 + i * 16;
    header[entry] = sizes[i] === 256 ? 0 : sizes[i];
    header[entry + 1] = header[entry];
    header.writeUInt16LE(1, entry + 4);
    header.writeUInt16LE(32, entry + 6);
    header.writeUInt32LE(image.length, entry + 8);
    header.writeUInt32LE(offset, entry + 12);
    offset += image.length;
  });
  fs.writeFileSync(path.join(assets, 'EzAcrossControlOFF.ico'), Buffer.concat([header, ...images]));
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
