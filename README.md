# Prefab Scatter

Een Unity editor-tool om prefabs (planten, stenen, gras, rommel...) te verspreiden binnen een zelfgetekend, organisch gebied. Alles komt netjes op de grond te staan. Geïnspireerd op [Greebler](https://github.com/alexismorin/Greebler).

- Unity **2019.4** of nieuwer
- Werkt met elke render pipeline (de tool spawnt alleen prefabs)
- Alleen editor-tijd: in een build zit niets extra's behalve het (lege) data-component

## Features

- **Prefab-lijst** met per prefab: weight (kans), scale min/max, align to normal en Y-offset
- **Drag & drop**: sleep een hele selectie prefabs in één keer in de inspector
- **Organisch gebied**: punten met een vloeiende Catmull-Rom-rand, concave vormen werken ook
- **Op de grond plaatsen** met raycasts naar beneden, alleen op de gekozen **Ground Layers** (meerdere layers mogelijk)
- **Density** per m², **Min Distance** tussen objecten, **Edge Falloff** voor een zachte rand en optionele **Perlin noise** voor plukken en open plekken
- **Max slope**: steile plekken worden overgeslagen
- **Willekeurige Y-rotatie** (0-360°)
- **Netjes in de hiërarchie**: alles komt onder één child-object (`Scattered`)
- **Deterministisch**: zelfde seed + zelfde instellingen = zelfde resultaat. **New seed** voor een nieuwe variatie
- **Volledige Undo-ondersteuning**
- **Visuele feedback** in de Scene view:
  - gevuld gebied met een omlijning die het terrein volgt
  - live preview van de plaatsingen (één kleur per prefab), al vóór je op Scatter drukt
  - paneel linksboven met Edit / Scatter / Clear / New seed en de aantallen

## Installatie

### Via git URL (aanbevolen)

*Window > Package Manager* → **+** → **Add package from git URL...**

```
https://github.com/PauseFish/prefab-scatterer.git
```

Een vaste versie gebruiken? Zet de tag erachter:

```
https://github.com/PauseFish/prefab-scatterer.git#v1.0.0
```

Of voeg het direct toe aan `Packages/manifest.json`:

```json
"dependencies": {
  "com.thesourcegame.prefabscatter": "https://github.com/PauseFish/prefab-scatterer.git#v1.0.0"
}
```

> Is de repo private? Dan moet git op je computer kunnen inloggen bij GitHub (bijv. via GitHub Desktop of de Git Credential Manager).

### Updaten

Unity onthoudt de commit in `Packages/packages-lock.json`. Een nieuwe versie haal je op door de tag in de URL/manifest aan te passen (bijv. `#v1.1.0`). Zonder tag kan je de regel voor deze package uit `packages-lock.json` weghalen.

### Lokaal aanpassen

Wil je de tool in een project zelf aanpassen, clone de repo dan in `<Project>/Packages/`. Unity gebruikt hem dan als embedded package die je gewoon kunt bewerken.

## Gebruik

1. **GameObject > The Source Game > Prefab Scatter Area**. Er verschijnt een onregelmatige cirkel op de grond bij het midden van je Scene view.
2. Voeg prefabs toe via de lijst of sleep ze in het vak **"Sleep prefabs hierheen"**.
3. Kies de **Ground Layers**. De grond moet een **collider** hebben (MeshCollider, TerrainCollider...).
4. Klik **Area bewerken** en vorm het gebied:

   | Actie | Invoer |
   |---|---|
   | Punt verplaatsen (over de grond) | Slepen |
   | Punt toevoegen | Shift + klik |
   | Punt verwijderen | Ctrl/Cmd + klik op een punt |
   | Stoppen met bewerken | Esc |

5. Stel **Density**, **Min Distance**, **Edge Falloff** en eventueel **Noise** in. De preview laat meteen zien wat er gebeurt.
6. Klik **Scatter**. Opnieuw klikken bouwt alles opnieuw op. **Clear** haalt alles weg.

### Instellingen

| Instelling | Uitleg |
|---|---|
| Weight | Relatieve kans dat een prefab gekozen wordt |
| Scale (min / max) | Willekeurige uniforme schaal, keer de schaal van de prefab |
| Align To Normal | 0 = recht omhoog, 1 = volledig mee met de helling |
| Y Offset | Verschuiving langs de omhoog-as. Negatief = iets in de grond zakken |
| Ground Layers | Alleen colliders op deze layers tellen als grond |
| Max Slope | Steilere plekken (in graden) worden overgeslagen |
| Raycast Height | Hoe ver boven het gebied de raycast start |
| Smooth Outline / Subdivisions | Vloeiende rand door de punten |
| Density | Maximaal aantal objecten per m² |
| Min Distance | Minimale afstand tussen twee objecten (0 = uit) |
| Edge Falloff | Afstand vanaf de rand waarover de dichtheid afloopt naar 0 |
| Use Noise / Scale / Threshold | Perlin noise voor plukken. Hogere threshold = meer open plekken |
| Random Y Rotation | Willekeurige rotatie rond Y |
| Seed | Bepaalt de willekeur |
| Max Instances | Veiligheidslimiet per gebied |
| Container Name | Naam van het child-object met alle gespawnde objecten |

### Tips

- De **pivot** van je prefabs hoort onderaan te zitten. Die komt op het raakpunt met de grond.
- Gespawnde objecten worden genegeerd bij de raycasts, ook als ze op een ground layer staan.
- Gebruik meerdere areas voor lagen (bijv. één voor gras, één voor struiken met een grotere Min Distance).
- **Punten op grond leggen** zet alle controlepunten op het terrein, handig nadat je het object hebt verplaatst.

## Structuur

```
package.json
Runtime/
  PrefabScatterArea.cs      Component met alle instellingen (alleen data + gizmo)
  ScatterMath.cs            Polygon-wiskunde op het XZ-vlak (smoothing, inside-test, triangulatie)
Editor/
  PrefabScatterGenerator.cs Berekent plaatsingen (raycasts) en spawnt/cleart met Undo
  PrefabScatterAreaEditor.cs Inspector, scene-handles, preview en scene-overlay
```

Namespace: `TheSourceGame.PrefabScatter`.
