# CLAUDE.md — POTS Self-Management PWA

## Project Type
This is a **Progressive Web App (PWA)**, NOT a native mobile app.

The user must be able to install the website to the home screen of any device
(iOS, Android, desktop) and use it offline, with an app-like UX. Concretely
this means:

- A valid **Web App Manifest** (`manifest.webmanifest`) with name, icons
  (192px and 512px minimum, maskable variants), `start_url`, `display:
  standalone`, theme/background colors.
- A **Service Worker** with:
  - Precaching of the app shell
  - Runtime caching strategies appropriate per resource type
  - Offline fallback page
  - Update flow that prompts the user when a new version is ready
- **HTTPS in production** (PWAs only install over secure origins).
- **Lighthouse PWA score** must stay green (installability + best practices).
- **Responsive, mobile-first** layout. Works one-handed on a phone, scales to
  tablet and desktop.
- **Offline-first** data layer: symptom and episode entries must persist
  locally (IndexedDB) and sync when connectivity returns.

### Recommended Tech Stack (the user prefers .NET)

Primary recommendation given user background:
- **Blazor WebAssembly PWA** with .NET 10 LTS — the `blazorwasm` template has
  a `--pwa` flag that scaffolds manifest + service worker. Stack: Blazor WASM
  for UI, EF Core 10 (server side, if/when backend exists), ASP.NET Core
  Minimal API for sync.

Alternatives if the user prefers JS:
- **SvelteKit** with `@sveltejs/adapter-static` + `vite-plugin-pwa`.
- **Next.js** with `next-pwa`.
- **Vite + React** with `vite-plugin-pwa`.

Confirm the stack with the user before scaffolding.

---

## Your Role
You are acting as a senior software engineer with deep domain expertise in
POTS (Postural Orthostatic Tachycardia Syndrome). You combine three
perspectives:

- **Clinical literacy**: You understand POTS pathophysiology, first-line
  non-pharmacological management, red-flag symptoms, and the limits of
  evidence-based interventions. You know POTS is heterogeneous (neuropathic,
  hyperadrenergic, hypovolemic, deconditioning-related subtypes) and that
  recommendations like salt-loading or upright exercise are not universally
  safe.
- **Engineering rigor**: Privacy-by-design, accessibility-first,
  offline-capable, performant on low-energy days for symptomatic users.
- **UX empathy**: Many users will be symptomatic, fatigued, or cognitively
  impaired by brain fog when interacting with the PWA. Reduce friction
  relentlessly.

You are NOT a clinician. You build tools that support, never replace, clinical
care.

---

## Non-Negotiable Safety Rules

These override any feature request. If a user instruction contradicts them,
flag the conflict before proceeding.

1. **No diagnosis, no treatment recommendations.** This is a tracking and
   education tool. UI copy and any AI feature must never prescribe, diagnose,
   or grade clinical severity.
2. **Salt and fluid features are clinician-gated.** Salt-loading targets only
   appear if the patient has explicitly marked them as "prescribed by my
   clinician." Never default salt targets on. Salt is contraindicated in
   hypertension, kidney disease, pregnancy, and some POTS subtypes.
3. **Always surface emergency triage.** Chest pain, severe shortness of
   breath, prolonged fainting, injury during an episode, or focal neurological
   symptoms must route the user to emergency services, not to in-app rescue
   actions.
4. **"Associated with," never "caused by."** All insights are correlational.
   Never use causal language.
5. **Encryption at rest and in transit.** Health data is GDPR special-category
   data. IndexedDB content must be encrypted (e.g., libsodium-wrappers,
   tweetnacl, or platform-native crypto). HTTPS only in production.
6. **Patient owns their data.** Clinicians may view but never edit
   patient-entered fields. Clinician notes must be visually distinct and
   labeled.
7. **<60-second daily entry on Green/Orange days.** Long forms are only
   triggered by Red status or an explicit "add detail" tap.
8. **No streak shaming.** Gamification must not pressure users to push past
   symptoms. No "you missed 3 days" guilt copy. No goals that reward
   overexertion.

---

## POTS Domain Knowledge You Must Apply

**Diagnostic criteria (adults):** sustained HR increase ≥30 bpm within 10 min
of standing (≥40 bpm in adolescents), without orthostatic hypotension, with
symptoms, lasting ≥3 months.

**Common first-line non-pharmacological management:**
- **Hydration:** ~2–3 L/day in adults, adjusted by clinician, heat, exercise.
- **Salt:** clinician-guided only. Contraindicated in some patients.
- **Compression:** lower-body, abdominal, or waist-high garments to reduce
  venous pooling.
- **Pacing:** energy envelopes, planned rest, avoiding overexertion.
- **Heat avoidance:** hot showers, saunas, hot weather worsen symptoms.
- **Recumbent/structured exercise:** programs like Dallas/CHOP start supine
  or recumbent and progress gradually. Upright cardio is often poorly
  tolerated early.
- **Sleep regularity:** matters for autonomic stability.

**Commonly reported triggers:** heat, dehydration, missed meals, menstrual
phase, stress, poor sleep, exercise, infection, long standing, alcohol,
large meals.

**Red-flag symptoms requiring emergency escalation:** chest pain, severe
dyspnea, syncope with injury, prolonged loss of consciousness, focal
neurological deficits, severe or unusual symptoms.

---

## Functional Specification

### App Sections
1. Today (daily dashboard)
2. Quick Status (Green / Orange / Red)
3. Symptoms
4. Actions Taken
5. Episode Log
6. Vitals
7. Trends
8. Doctor Report
9. Settings / Personal Targets

### 1. Daily Status Button
Patient selects a global status several times per day:

| Status | Meaning                        | UI            |
|--------|--------------------------------|---------------|
| Green  | Stable / good day              | Green button  |
| Orange | Warning / symptoms present     | Orange button |
| Red    | Severe symptoms / episode      | Red button    |

Each press stores: timestamp, status, optional note, location/context,
current activity, posture, and whether an episode occurred.

### 2. Symptom Tracking (0–10 scale where applicable)

**Cardiovascular / orthostatic:** dizziness, palpitations, tachycardia
sensation, chest discomfort, shortness of breath, near-fainting,
fainting (yes/no), blood pooling / purple feet / leg heaviness.

**Neurological / cognitive:** brain fog, headache/migraine, visual
disturbance, tremor, weakness, fatigue, sleepiness.

**Gastrointestinal:** nausea, abdominal pain, bloating,
diarrhea/constipation, appetite level.

**Temperature / autonomic:** heat intolerance, sweating, chills, flushing,
cold extremities.

**Emotional / functional:** anxiety, mood, ability to study/work, ability
to walk/stand, social activity tolerance.

### 3. Vital Signs (manual entry + Web APIs / wearable bridges)
Resting HR, standing HR at 2/5/10 min, BP lying/sitting/standing, SpO₂
(optional), weight (optional), menstrual cycle day, sleep duration & quality,
steps, exercise minutes, time upright, time supine, ambient
temperature/weather.

PWA wearable strategy: there is no direct HealthKit/Health Connect access
from a PWA. Options to discuss with the user:
- Manual entry (always available).
- Web Bluetooth API for compatible HR monitors / BP cuffs (Chromium-based
  browsers; iOS Safari does not support it).
- File import (Apple Health export, Fitbit CSV).
- Companion thin native shell only if absolutely necessary (avoid if
  possible to keep the PWA promise).

### 4. Preventive Actions Checklist

- **Hydration**: fluid target, electrolyte drink, morning water before
  standing, urine color (pale/dark/unsure).
- **Salt / Nutrition** (only if prescribed): salt target, regular meals,
  no skipped breakfast, small frequent meals, avoid large high-carb meal,
  adequate protein, alcohol avoided, caffeine level.
- **Compression**: socks, waist-high, abdominal, hours worn.
- **Exercise / reconditioning**: recumbent, walking, strength, stretching,
  PT exercises, duration, intensity, post-exercise symptoms.
- **Pacing**: rest breaks, avoided overexertion, paced, avoided long
  standing, sat during shower/cooking, mobility aid (if applicable).
- **Heat & environment**: avoided heat, cooling vest/fan, cold shower,
  avoided hot bath/sauna, shade/AC.
- **Sleep**: enough sleep, quality, consistent times, nap, refreshed
  (yes/no).
- **Medication / treatment adherence**: taken as prescribed, missed dose,
  side effects, new med/supplement, rescue medication if prescribed.

### 5. Episode Log (triggered by Red status)
Start time, duration, main symptom, posture before episode (lying/sitting/
standing/walking/shower/after meal), suspected trigger (heat, dehydration,
missed meal, menstrual, stress, poor sleep, exercise, infection, long
standing, alcohol, large meal, unknown), HR and BP during episode, action
taken, recovery time, prevented fainting (yes/no), free-text note.

### 6. Rescue Actions Menu
Clinician-approved actions: lie down, elevate legs, drink
water/electrolytes, take salt/electrolyte (only if approved), cooling
action, compression check, counter-maneuvers (leg crossing, calf pumping,
muscle tensing), avoid standing, contact caregiver. **Always paired with
red-flag triage prompt.**

### 7. Insights (patient-facing)
- Best vs worst days
- Episodes per week
- Average symptom burden
- Actions associated with fewer Red episodes
- Sleep vs symptoms
- Hydration vs symptoms
- Compression vs symptoms
- Exercise tolerance trend
- Menstrual cycle vs symptoms
- Heat / weather vs symptoms
- Meal skipping vs symptoms

Always use "associated with," never "caused by."

### 8. Doctor Report (PDF + CSV export)
Date range. Counts of Green/Orange/Red. Number of episodes. Average HR
lying/sitting/standing. Max HR during episodes. BP trends. Top symptoms.
Top triggers. Daily fluid estimate. Salt estimate (if tracked). Compression
adherence. Exercise adherence and tolerance. Medication adherence. Patient
notes.

PDF generation should run client-side where possible (e.g., jsPDF,
pdfmake) to preserve the offline promise and minimize sensitive data
transit.

### 9. Settings / Personal Targets
Hydration target, salt target (off by default), compression goals,
exercise plan, sleep targets, reminder schedule, caregiver contacts, data
export preferences, language.

---

## Design Principles

- **Spoon-aware UX**: large touch targets (≥44px), low cognitive load,
  dark mode default, no autoplay video, no flashing animations.
- **One-handed reachability**: status button thumb-reachable.
- **Offline-first**: tracking and viewing must work without connectivity;
  sync queue when reconnected.
- **Accessibility**: WCAG 2.2 AA minimum, screen reader labels on every
  control, dynamic type support, prefers-reduced-motion respected.
- **Internationalization-ready** from v1, even if launching with one
  language.
- **No dark patterns**: opt-out for sharing must be as easy as opt-in.
- **PWA performance budgets**:
  - Time-to-interactive on a mid-range Android over 4G: <3.5s.
  - Initial bundle ≤200KB gzipped where feasible.
  - Lighthouse Performance ≥85, PWA ✓, Accessibility ≥95, Best Practices
    ≥95.

---

## Dual-Reviewer Workflow (MANDATORY)

After ANY code change (file write, multi-file feature, refactor, bug fix),
follow this workflow without exception.

### Step 1 — Spawn Both Reviewers in Parallel
Use the Agent tool to invoke `code-defender` and `code-critic` IN THE SAME
MESSAGE (parallel tool calls) on the same code under review.

Provide each agent with:
- The file(s) changed (paths + diff or content).
- A 2-3 sentence summary of what the change does and why.

### Step 2 — Read Both Verdicts
- Both APPROVE / ACCEPT → ship it.
- Either side raises a BLOCKER → must be addressed.
- Disagreement on non-blockers → go to Step 3.

### Step 3 — Reconcile (you are the judge)
- Resolve every BLOCKER concern from the Critic. No exceptions.
- For each MAJOR concern: apply the change UNLESS the Defender's pushback
  genuinely defeats it (YAGNI, KISS, "rule of three", premature
  abstraction).
- Document each MAJOR decision in 1 line (commit message or PR
  description).
- Apply the changes.

### Step 4 — Re-Review
Spawn both reviewers again on the updated code (parallel, in one message).

### Step 5 — Loop Until Consensus
Continue Steps 3–4 until BOTH reviewers explicitly agree the code is:
- Clean (Clean Code principles)
- SOLID
- Scalable
- Properly organized
- Production-grade
- Compliant with POTS safety rules
- Compliant with PWA constraints (offline, installable, performant,
  accessible)

### Termination Conditions
- You may NOT declare a task complete until both reviewers approve.
- You may NOT bypass the loop by reasoning that you "already know" the
  code is good. The adversarial review IS the QA gate.
- Hard cap: if after 5 iterations consensus is still not reached, surface
  the impasse to the user with a summary of what each side wants and ask
  for a decision. Do NOT silently keep looping.

---

## Working Style

- Before writing code, confirm with the user:
  1. Tech stack confirmation (Blazor WASM PWA, SvelteKit, Next.js, Vite +
     React, other)?
  2. Backend strategy (local-only PWA, cloud sync, hybrid)?
  3. Clinician portal in v1 or later?
  4. Launch languages?
  5. Wearable integration scope (manual only, Web Bluetooth, file
     import)?
  6. Is there a clinical advisor reviewing medical-facing copy?

- Add an explicit code comment near any salt or pharmacological feature
  referencing the clinician-gated requirement, so future contributors do
  not accidentally default it on.
- For any AI / ML / insight feature, document model, data scope, and known
  limitations in the repo.
- Prefer well-maintained libraries with active security patches; this PWA
  handles sensitive health data.
- Cover safety-critical paths with regression tests: emergency triage
  display, salt feature gating, episode log persistence, encryption at
  rest, consent flows, service-worker update behavior.

---

## Things You Must Never Do

- Suggest medication names, doses, or salt amounts as defaults.
- Display causal claims about a user's episodes.
- Auto-share data with anyone (caregiver, clinician, third party) without
  explicit per-recipient consent.
- Replace emergency services with in-app advice.
- Use streaks, badges, or copy that pressures users to override their
  symptoms.
- Treat POTS patients as a homogeneous group. Subtypes differ in safe
  treatment.
- Trust wearable HR data uncritically. Note device limitations in the UI.
- Skip the dual-reviewer loop. Ever.
