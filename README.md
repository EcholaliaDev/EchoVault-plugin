# EchoVault — Echo Plugin

**Echo** is the Dalamud plugin for [EchoVault](https://echovault.gg) — a crowd-sourced community index of FFXIV players you cross paths with.

---

## What is EchoVault?

EchoVault is a community index of Final Fantasy XIV player sightings, built from what contributors' game clients can already see. When you install Echo, characters you encounter in the world are recorded and synced to the shared index. Anyone can browse the index at [echovault.gg](https://echovault.gg) to see who they have crossed paths with.

EchoVault records identity snapshots — name, home world, level, job, title, free company — and zone-level presence. It deliberately stores **no positions and no movement paths** — only which zone a character was last seen in. No private account data, no chat, no personal information beyond what other players can already observe in-game.

---

## Install

1. Open **Dalamud Settings** (type `/xlsettings` in-game, or click the Dalamud logo).
2. Go to the **Experimental** tab.
3. Under **Custom Plugin Repositories**, paste the following URL and click the **+** button:

   ```
   https://raw.githubusercontent.com/EchoVaultDev/EchoVault-plugin/main/repo.json
   ```

4. Click **Save & Close**.
5. Open the **Plugin Installer** (`/xlplugins`), search for **Echo**, and click **Install**.

---

## What data does the plugin capture?

Echo records only what your game client can already see when you share a zone with other players:

- Character name and home world
- Level, job, title, free company tag
- Grand company affiliation
- Appearance/equipment snapshots
- The zone the character was last seen in
- Timestamps

It captures **no chat**, **no positions**, **no movement paths**, and **no private or account data**.

Full details are at [echovault.gg/privacy](https://echovault.gg/privacy).

---

## Opt-out and removal

Character owners can [claim their character](https://echovault.gg/me) for self-service privacy controls — hide your profile entirely, hide alt links, hide location and recency, or hide history.

To request removal of a profile, use the removal form at:

**[echovault.gg/removal](https://echovault.gg/removal)**

Every removal request is reviewed. Hiding and removal take effect within minutes of approval.

---

## Support

Found a bug or have a question? [Open a GitHub Issue](https://github.com/EchoVaultDev/EchoVault-plugin/issues/new/choose) in this repository.

For data or removal requests, use [echovault.gg/removal](https://echovault.gg/removal) — do not post identifying details in a public issue.
