# Dino-Sprites

Die Figur wurde aus der vom Benutzer bereitgestellten Referenz als konsistentes 4×2-Atlas abgeleitet. Der magentafarbene Generierungsstand liegt unter `Source`; die produktiven Zustandsbilder besitzen einen Alpha-Kanal.

Die Zuordnung ist vollständig datengetrieben in `sprites.json`. Zusätzlich zu den Grundposen existieren Bildfolgen für Blinzeln, Schwanzwackeln und Gewichtsverlagerung. Weitere Frames können je Zustand ergänzt werden, ohne UI-Code zu ändern. `DinoSpritePlayer` aktiviert seinen Timer nur für Sequenzen mit mehr als einem Frame.

Der eingebaute WPF-Vektor bleibt ausschließlich als technischer Fallback erhalten, wenn ein Bild fehlt oder nicht geladen werden kann.
