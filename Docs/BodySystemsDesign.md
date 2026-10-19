# Pulse Vector: travelling through the other body systems

This is the plan for taking the medical vessel beyond the artery: the respiratory, nervous, digestive and other systems. It follows the mission list in `gamedesc.txt` (Arterial Descent, Cardiac Maelstrom, Pulmonary Refit, Neural Corridor, Lesion Strike). It also reuses what the artery mission already has: the vessel and its crew, wall containment, cells you cure or destroy, wall damage fixed with the right tool, blood loss, and sounds.

## 1. The big idea: the bloodstream is the highway

Blood reaches every organ, so the vessel always enters a new system the way a real nanodevice would: through the bloodstream, then across into the organ.

| Route | How the vessel crosses | Gameplay at the crossing |
|---|---|---|
| Artery → heart | Straight in through the vena cava | Time your entry to the heartbeat |
| Heart → lungs | Pulmonary artery → capillaries → alveolus | Capillaries are barely wider than the hull: squeeze through, with the hull scraping |
| Bloodstream → brain | Cross the blood–brain barrier | Docking puzzle: match the barrier's "key" (sensor power plus a timed beam) |
| Bloodstream → gut | Portal vein, or out through an ulcer in the gut wall | Choose a route: the safe long way or the risky short way |
| Bloodstream → kidney | Renal artery → glomerulus filter | A filter gate that only opens if the hull is small enough (a shrink module upgrade) |
| Any system → exit | Back into a vein, out to the extraction point | The extraction timer from the game description |

Between missions, a **body map** acts as the hub. It shows the patient's outline with each system lit up as a mission, plus the route between them, played as a short "transit" flight down a vein.

## 2. The systems

Each system has its own environment look, a **flow** (what pushes the vessel around), threats, conditions to treat, and at least one new tool or ship-system twist.

### Cardiovascular: heart (Mission 2, Cardiac Maelstrom)
- **Look:** four huge, pulsing chambers with fibrous red walls. The valves are flaps that open and shut with the heartbeat.
- **Flow:** strong surges on every beat, then a lull. Fly during the lull.
- **Threats:** turbulence and pressure spikes. An arrhythmia event makes the beat irregular, so timing gets hard.
- **Conditions:**
  - Faulty valve: stitch the leaflet with the **suture**.
  - Clot in the chamber: **medicine**.
  - Scar tissue causing the arrhythmia: **laser** (ablation, like a real cardiac procedure).
- **New twist:** pressure gates. Valves are doors that are only open for a moment each beat.

### Respiratory: trachea → bronchi → alveoli (Mission 3, Pulmonary Refit)
- **Look:** pale pink airway with ribbed cartilage rings. It branches like a tree, getting narrower each time, and ends in grape-like air sacs (alveoli) wrapped in capillaries.
- **Flow:** breathing. Air rushes in, then out, on a slow cycle. Cilia (tiny hairs) carry a mucus "conveyor" back up toward the throat, so the vessel is pushed backwards unless it keeps thrust on.
- **Threats:** bacteria and viruses (cells to destroy), and dust or smoke particles that clog the sensors.
- **Conditions:**
  - Mucus plug: **mucolytic medicine** (medicine round).
  - Inflamed airway that narrows (asthma): **bronchodilator**, a new spray tool that widens the passage over time, like the balloon but gas-based.
  - Tumour on the wall: **laser**, carefully. Healthy tissue next to it counts as "curable cells lost" if you hit it.
  - Collapsed alveolus: **balloon** to re-inflate it.
- **Oxygen and stealth**, from the mission name: in low-oxygen zones the crew needs the oxygen from an alveolus, so you dock to refill. Immune cells here hunt by movement, so cutting the engines lets you drift past (stealth).

### Nervous: spinal canal → nerve → brain (Mission 4, Neural Corridor)
- **Look:** clear, glowing cerebrospinal fluid in the spinal canal, then bundles of axons like cables wrapped in fatty myelin sleeves, then synapses (gaps between nerve endings). It's dark, with bioluminescent blue and purple light.
- **Flow:** calm fluid, but electrical **action potentials** race along the axons as bright pulses. A pulse hitting the vessel scrambles the sensors (the existing ShipSystems sensor disruption). Riding just behind a pulse gives a speed boost.
- **Threats:** electrical storms (seizure activity), plus microglia that attack anything foreign.
- **Conditions:**
  - Damaged myelin (as in multiple sclerosis): a new **myelin wrap** beam tool that coats the axon, applied like the stent.
  - Brain aneurysm: **stent**.
  - Stroke clot: **medicine** under a strict timer, because brain tissue dies quickly.
  - Severed nerve: **suture** at the synapse, timed between pulses.
- **Precision:** narrow corridors where the steering assist is weaker, and the walls cost hull integrity on contact.

### Digestive: oesophagus → stomach → intestines
- **Look:** pink, ridged oesophagus, then the cavernous stomach with folded walls and a pool of acid, then the intestines lined with a forest of finger-like villi.
- **Flow:** **peristalsis**, squeezing waves that travel down the tube. Ride a wave, or get crushed between two.
- **Threats:** stomach acid damages the hull unless you put power into Shields. Bad bacteria are mixed in with good gut bacteria.
- **Conditions:**
  - Ulcer: **suture**, plus medicine to protect it.
  - Polyp: **laser**.
  - Bowel blockage: **balloon**.
  - Swallowed foreign object: **grabber**.
- **Strategy:** the gut's good bacteria are "curable cells" in reverse. Destroying them hurts the patient, so aim carefully in the crowded villi.

### Others (later missions)
- **Kidney/urinary:** kidney stones broken up with the laser (lithotripsy). The filter gates need the shrink module.
- **Lymphatic:** lymph nodes as safe resupply bases where friendly immune cells repair the hull, with a slow, clear flow.
- **Liver:** detox puzzle. Clear toxin deposits with medicine before they spread.
- **Bone marrow:** a crumbly honeycomb maze where new blood cells are made. An escort mission protects stem cells.

## 3. How the code gets there (technical approach)

What exists now is built around one artery. These steps turn it into a reusable engine for any body passage, without breaking the current mission.

1. **Passages instead of one artery.** Turn `ArteryPath` (a hard-coded centreline and radius) into a `Passage` that holds a spline centreline, a radius-along-length curve, and a cross-section shape (round, D-shaped, ribbed, folded). A `PassageProfile` asset (ScriptableObject) holds the look: wall texture settings, colours, relief pattern, fog, lighting and flow. The artery becomes the first profile, so containment, the pilot, camera, blood and spawning all keep working unchanged.
2. **Branches and chambers.** A `PassageNetwork` joins passages at junctions (the bronchial tree, intestine loops) and adds large open volumes (heart chambers, the stomach) as separate shapes. Containment then works on "inside any passage or chamber" rather than one tube.
3. **Flow fields.** A `FlowField` per passage gives the push at any point: steady (blood), pulsing (heartbeat), reversing (breathing), backwards (mucus) or travelling waves (peristalsis). It applies to the vessel, the floating cells and particles, and the sound pitch.
4. **Conditions as data.** The existing lesions and diseased cells become `ConditionDefinition` assets: appearance, which tool cures it, a timer, and the consequence of ignoring it (bleeding, bursting, spreading). New conditions, such as a mucus plug or damaged myelin, are new assets plus a small visual script. The tool check, labels, sounds and win/lose scoring already work this way.
5. **Tools as modules.** `SurgicalBeam` already switches tools. New tools (bronchodilator spray, myelin wrap) become loadout modules chosen at the briefing, which matches the upgrade system in the game description.
6. **One scene per system.** Load scenes additively, with a persistent "patient" object that carries vitals across them: blood loss, oxygen, infection level, time left. The body map and transit flights load and unload them.
7. **Hazards that drain ship systems.** Acid drains Shields, nerve pulses scramble Sensors, mucus slows Propulsion. This uses the existing `ShipSystems` power routing, which gives the "boost one system, weaken another" strategy from the design pillars.

## 4. Build order (milestones)

| # | Milestone | What you can play when it's done |
|---|---|---|
| 1 | Passage refactor | The same artery, now built from a `PassageProfile`, plus a second, branching artery segment |
| 2 | Heart (Mission 2) | Pulsing chambers, valve gates, heartbeat flow, three cardiac conditions |
| 3 | Lungs (Mission 3) | Bronchial tree, breathing flow, mucus conveyor, alveolus oxygen docking, stealth immune cells |
| 4 | Body map and transit | Pick a mission on the patient outline and fly a vein to it |
| 5 | Brain and nerves (Mission 4) | Glowing neural corridor, action-potential pulses, myelin wrap tool |
| 6 | Gut | Peristalsis, acid and shields, good versus bad bacteria |
| 7 | Finale (Mission 5) | Lesion strike and extraction, using everything above |

Each milestone ends playable on desktop and in VR, with the cockpit view, sounds and crew working in the new environment.

## 5. Decisions needed from you

- **Next system:** the lungs (Mission 3) give the most visual contrast, while the heart (Mission 2) follows the story order.
- **Structure:** one long mission per system, or shorter "patient cases" that each visit two or three systems.
- **Fidelity:** how true to life the anatomy should be, either recognisably real (based on medical imagery) or stylised for clarity.
