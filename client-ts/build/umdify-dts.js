// Edit the index.d.ts file to add the UMD export
const fs = require('fs');
const path = require('path');

if (process.argv.length !== 4) {
    console.error("usage: node ./umdify-dts.js <target file> <namespace>");
    process.exit(1);
}

let target = process.argv[2];
let ns = process.argv[3];

let content = fs.readFileSync(target);
fs.writeFileSync(target, content + `\r\nexport as namespace ${ns};`);