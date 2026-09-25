Relic for TrenchBroom
====================

This directory is the Relic game definition for TrenchBroom.
After installation, Relic appears in TrenchBroom -> Select Game
beside Quake, Quake 2, Half-Life and Hexen 2. No Generic mode,
no manual configuration, no file copying is required - run the
RelicTrenchBroomInstaller (Relic.Content) or press "Install
TrenchBroom Game Files" in the Relic launcher.

Contents
--------
GameConfig.cfg          TrenchBroom game configuration (Valve 220,
                        textures from Assets/Textures, entities
                        from Assets/Relic.fgd, maps export to
                        Assets/Maps).
Relic.fgd               Copy of Assets/Relic.fgd (entity definitions).
Icon.png                Relic game icon shown in the game list.
EntityGroups.cfg        Entity browser categories (Player, Enemies,
                        Pickups, Props, Lights, Environment,
                        Triggers).
FaceAttribs.cfg         Default face attributes + material groups
                        (brick, metal, concrete -> Assets/Materials).
CompilationProfiles.cfg Map workflow (Relic loads .map directly,
                        no compilation step).

Layout expected by TrenchBroom
------------------------------
<TrenchBroom>/games/Relic/
    GameConfig.cfg
    Icon.png
    Relic.fgd

MDL previews and palette
--------------------------
Quake MDL skins are 8-bit indexed color. TrenchBroom resolves
the palette through "textures": { "palette": "gfx/palette.lmp" }
in GameConfig.cfg, relative to the game filesystem. The authentic
Quake palette ships at Assets/gfx/palette.lmp (768 bytes). Keep
that file and the palette key in place or MDL previews
(dog.mdl, demon.mdl, soldier.mdl, player.mdl) fail with
"Could not load palette file". The Relic engine itself
(MDLLoader) reads the same file so game and editor agree.

The installer resolves <TrenchBroom> automatically:
  - RELIC_TRENCHBROOM_DIR environment variable
  - <repo>/TrenchBroom-Win64-*/games
  - %ProgramFiles%/TrenchBroom/games
  - %LocalAppData%/TrenchBroom/games
  - Steam TrenchBroom install

Map workflow
------------
1. New Map -> select a template from Assets/Templates/
   (basic_room, combat_test, lighting_test, shadow_test).
2. Map Format: Valve 220. Game: Relic.
3. Place entities from the Relic groups. Models in
   Assets/Models show as previews (crate.obj, soldier.mdl,
   dog.mdl, demon.mdl); anything else shows a bounding box.
4. Textures come from Assets/Textures, grouped under Relic
   (brick, metal, concrete). Materials in Assets/Materials
   supply diffuse/normal/specular/roughness/tint.
5. Save the .map into Assets/Maps/. Relic loads it directly -
   new entities, models, textures, materials and sounds work
   with no code changes.
