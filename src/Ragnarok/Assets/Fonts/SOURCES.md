# Font sources and licenses

Rune & Iron guide fonts added 2026-10-19 from the Google Fonts repository:

- Cinzel: https://github.com/google/fonts/tree/main/ofl/cinzel
  Copyright 2020 The Cinzel Project Authors. SIL OFL 1.1; see `Cinzel-OFL.txt`.
  Upstream file: `Cinzel[wght].ttf`. The shipped `Cinzel-SemiBold.ttf` is a static
  instance at weight 600, generated with fontTools.varLib.instancer for Unity's
  runtime font loader. Its names identify it as Cinzel SemiBold. No glyph artwork
  was edited. A variable font is not required at runtime.
- Barlow Semi Condensed: https://github.com/google/fonts/tree/main/ofl/barlowsemicondensed
  Copyright 2017 The Barlow Project Authors. SIL OFL 1.1;
  see `BarlowSemiCondensed-OFL.txt`. Original static Regular (400), SemiBold (600),
  Bold (700) and Italic (400) files are shipped without modification.
- Noto Sans Runic: https://github.com/google/fonts/tree/main/ofl/notosansrunic
  Copyright 2022 The Noto Project Authors. SIL OFL 1.1;
  see `NotoSansRunic-OFL.txt`. Original static Regular file, unmodified.

Font files and OFL license texts are embedded in Ragnarok.dll so redistribution
retains the licenses. Dynamic TMP atlases and materials follow FontLibrary's
existing unload cleanup. Static font instances need no additional runtime dependency.
Alegreya and Scada assets from the earlier implementation retain their existing
`Alegreya-OFL.txt` and `Scada-OFL.txt` licenses.

Shipped file SHA-256 values:

```
0dbbfb9ff718eec7c1890428a8c718610679ba94674b5a0ca70a4afb743e173c  Cinzel-SemiBold.ttf
e8c7242f612b13d026e18a94b63b0fd079e91a3faf009d1b6981dbb5f16d1153  BarlowSemiCondensed-Regular.ttf
bd299f4bc5b44d30be8d42e9bad3a5df7d66af1cd55d0ed72a8b8916360a1424  BarlowSemiCondensed-SemiBold.ttf
5bd6757e459a110da81b44532f0f43058e2dab2401688abca84f2fdbde193cbb  BarlowSemiCondensed-Bold.ttf
6ce28204c8a48369f254055fccf1784fe10ec7fcc26f84a300d5165ddfc0cb0a  BarlowSemiCondensed-Italic.ttf
615b6c1166a8c51816dc1536784acbf486547e3bed03dbfdd70910e10066793b  NotoSansRunic-Regular.ttf
```
