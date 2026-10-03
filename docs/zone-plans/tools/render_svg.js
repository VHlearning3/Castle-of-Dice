// node render_svg.js <svg_dir> <png_dir> [width] [height]  (needs the playwright package + Chromium)
const { chromium } = require('playwright');
const fs = require('fs'), path = require('path');
(async () => {
  const [src, dst, w = '1600', h = '1000'] = process.argv.slice(2);
  fs.mkdirSync(dst, { recursive: true });
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: +w, height: +h } });
  for (const f of fs.readdirSync(src).filter(f => f.endsWith('.svg')).sort()) {
    await p.setContent(`<html><body style="margin:0">${fs.readFileSync(path.join(src, f), 'utf8')}</body></html>`);
    await p.screenshot({ path: path.join(dst, f.replace('.svg', '.png')) });
  }
  await b.close();
})();
