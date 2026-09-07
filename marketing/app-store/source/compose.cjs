// Editable App Store artwork: real screenshots embedded unchanged in SVG layouts.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const opentype = require('opentype.js');
const root = path.resolve(__dirname, '..');
const repo = path.resolve(root, '../..');
const fontPath = path.join(repo, 'Assets/Resources/Brand/Rounded-Bold.ttf');
const font = opentype.loadSync(fontPath);
const colors = {ink:'#202344',blue:'#315DFF',orange:'#FF781F',paper:'#F0F6FC',muted:'#62718D'};
const img = f => 'data:image/png;base64,'+fs.readFileSync(path.join(root,f)).toString('base64');
function text(t,x,y,size,color=colors.ink,align='left') {
  if(align==='center') x-=font.getAdvanceWidth(t,size)/2;
  return font.getPath(t,x,y,size).toSVG().replace('<path ',`<path fill="${color}" `);
}
function label(t,x,y,w,color=colors.blue) {
  return `<rect x="${x}" y="${y}" width="${w}" height="64" rx="32" fill="${color}"/>`+text(t,x+w/2,y+44,32,'#FFFFFF','center');
}
function phone(file,x,y,w,angle=0) {
  const h=w*2868/1320, id='clip'+file.replace(/\W/g,'')+x;
  return `<g transform="rotate(${angle} ${x+w/2} ${y+h/2})"><rect x="${x-17}" y="${y-17}" width="${w+34}" height="${h+34}" rx="76" fill="#202344" filter="url(#shadow)"/><rect x="${x-10}" y="${y-10}" width="${w+20}" height="${h+20}" rx="69" fill="#FFFFFF"/><clipPath id="${id}"><rect x="${x}" y="${y}" width="${w}" height="${h}" rx="62"/></clipPath><image href="${img('captures/'+file+'.png')}" x="${x}" y="${y}" width="${w}" height="${h}" clip-path="url(#${id})"/></g>`;
}
function detail(file,box,x,y,w,h) {
  const id='detail'+file+x;
  return `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="44" fill="#FFFFFF" filter="url(#shadow)"/><svg x="${x+12}" y="${y+12}" width="${w-24}" height="${h-24}" viewBox="${box.join(' ')}" preserveAspectRatio="xMidYMid meet"><image href="${img('captures/'+file+'.png')}" width="1320" height="2868"/></svg>`;
}
const slides=[
 {id:'01-match',a:'Match the color.',b:'Beat the clock.',sub:'Four colors. One perfectly timed move.',layout:()=>phone('hero',190,690,940,-1.5)},
 {id:'02-modes',a:'Find your Flow.',b:'Feel the Rush.',sub:'Three chances to settle in. One chance to go all out.',layout:()=>label('FLOW · 3 CHANCES',72,770,545)+label('RUSH · 1 CHANCE',703,1080,545,colors.ink)+phone('flow',88,880,530,-2)+phone('rush',706,1190,530,2)+text('Two modes. Two ways to find your best.',660,2710,39,colors.ink,'center')},
 {id:'03-motion',a:'Stay sharp.',b:'Things move.',sub:'Floating pucks. Drifting rings. A fresh rhythm.',layout:()=>phone('moving',192,690,936,1.4)},
 {id:'04-perfect',a:'Make every',b:'match count.',sub:'Build your combo. Land a Perfect.',layout:()=>phone('perfect',340,730,780,1.5)+detail('results',[65,230,1190,2080],90,1850,455,815)},
 {id:'05-collection',a:'Make it',b:'yours.',sub:'Earn new finishes, rings, and a trail as you play.',layout:()=>phone('collection',188,675,944,-1)+detail('still',[80,860,1160,1600],810,2040,430,595)},
 {id:'06-your-pace',a:'Your pace.',b:'Your next best.',sub:'Choose a Still board. Play Flow and Rush offline.',layout:()=>phone('symbols',100,760,780,-1.7)+detail('settings',[75,500,1170,1850],785,1650,450,760)+label('SOUND · SYMBOLS · EFFECTS',270,2680,785,colors.ink)}
];
async function main(){
 fs.mkdirSync(path.join(root,'source/compositions'),{recursive:true});
 for(const s of slides){
   const svg=`<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="1320" height="2868" viewBox="0 0 1320 2868"><defs><filter id="shadow" x="-35%" y="-20%" width="170%" height="150%"><feDropShadow dx="0" dy="26" stdDeviation="28" flood-color="#202344" flood-opacity=".18"/></filter></defs><rect width="1320" height="2868" fill="${colors.paper}"/><image href="${img('artwork/sculpted-backdrop.png')}" width="1320" height="2868" preserveAspectRatio="xMidYMid slice"/><rect width="1320" height="640" fill="${colors.paper}" opacity=".68"/>${text('RING RUSH',100,160,43,colors.blue)}${[colors.blue,colors.orange,'#C5ED32','#FF3E87'].map((c,i)=>`<circle cx="${1060+i*48}" cy="141" r="15" fill="${c}"/>`).join('')}${text(s.a,100,330,128)}${text(s.b,100,469,128,colors.blue)}${text(s.sub,100,570,40,colors.muted)}${s.layout()}</svg>`;
   fs.writeFileSync(path.join(root,'source/compositions',s.id+'.svg'),svg);
   await sharp(Buffer.from(svg)).flatten({background:colors.paper}).removeAlpha().png().toFile(path.join(root,'screenshots',s.id+'.png'));
   console.log('Rendered',s.id);
 }
 const thumbs=await Promise.all(slides.map(s=>sharp(path.join(root,'screenshots',s.id+'.png')).resize(330,717).toBuffer()));
 await sharp({create:{width:1050,height:1514,channels:3,background:'#E4EBF4'}}).composite(thumbs.map((input,i)=>({input,left:15+(i%3)*345,top:20+Math.floor(i/3)*747}))).png().toFile(path.join(root,'contact-sheet.png'));
 await sharp(path.join(repo,'Assets/Art/AppIcon.png')).resize(1024,1024).flatten({background:colors.paper}).removeAlpha().png().toFile(path.join(root,'app-icon-1024.png'));
 const listing=JSON.parse(fs.readFileSync(path.join(root,'listing.json')));
 listing.description=listing.description.replace('Prefer a steady layout?','Prefer less motion?');
 fs.writeFileSync(path.join(root,'listing.json'),JSON.stringify(listing,null,2)+'\n');
 const human=`# Ring Rush — English (US) App Store copy\n\nLocal draft. Not saved to App Store Connect.\n\n## Name\n\n${listing.name}\n\n## Subtitle\n\n${listing.subtitle}\n\n## Promotional text\n\n${listing.promotionalText}\n\n## Description\n\n${listing.description}\n\n## Keywords\n\n${listing.keywords}\n\n## Initial release notes\n\n${listing.releaseNotes}\n`;
 fs.writeFileSync(path.join(root,'listing.md'),human);
}
main().catch(e=>{console.error(e);process.exit(1)});
