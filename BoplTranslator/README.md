# Bopl Translator

A new guid based translation system + custom translation support.

## Setup
Place translation txts into translations folder next to dll (or make new folder if it doesn't exists).

## Configs
Use `fallback language` to change which language to use when no translation is found for guid.

## English example translation
```
name = EN
font = English #This can be English, Japanese, Korean, Russian, Chinese or Polish

menu_play = play
play_start = start!
menu_online = online
menu_settings = settings
menu_exit = exit
settings_sfx_vol = sfx\nvol
settings_music_vol = music\nvol
settings_abilities = abilities
settings_screen_shake = screen shake      
settings_rumble = rumble
settings_resolution = resolution
settings_save = save
general_on = on
general_off = off
general_high = high
screen_fullscreen = fullscreen
screen_windowed = windowed
screen_borderless = borderless
settings_screen = screen
play_click = click to join!
play_ready = ready!
play_color = color
play_team = team
rebind_keys = rebind keys
rebind_jump = click jump
rebind_ability_left = click ability_left  
rebind_ability_right = click ability_right
rebind_ability_top = click ability_top    
rebind_move_left = click move_left        
rebind_move_down = click move_down        
rebind_move_right = click move_right      
rebind_move_up = click move_up
settings_vsync = vsync
hide_nothing = nothing
settings_hide = hide
hide_names = names
hide_names_avatars = names and avatars    
undefined_mouse_only = mouse only
play_local_game = local game
undefined_click_start = click to start!   
end_next_level = next level
end_ability_select = ability select       
end_winner = winner!!
end_winners = winners!!
end_draw = draw!
undefined_whishlist = wishlist bopl battle!
play_choosing = choosing...
pause_leave = leave game?
menu_invite = invite friend
undefined_practice = practice
tutorial_hold_dow = hold down
tutorial_aim = to aim
tutorial_throw_greneade = to throw grenade
tutorial_dash = to dash
tutorial_click = click
menu_credits = credits
credits_back = back
menu_tutorial = tutorial
play_empty_lobby = your lobby is empty
play_invite = invite a friend to play online
play_not_abailable_demo = not available in demo
play_find_players = find players
play_stop_player_search = stop search
item_bow = bow
item_tesla_coil = tesla coil
item_engine = engine
item_smoke = smoke
item_invisibility = invisibility
item_platform = platform
item_meteor = meteor
item_random = random
item_missile = missile
item_black_hole = black hole
item_rock = rock
item_push = push
item_dash = dash
item_grenade = grenade
item_roll = roll
item_time_stop = time stop
item_blink_gun = blink gun
item_gust = gust
item_mine = mine
item_revival = revival
item_spike = spike
item_shrink_ray = shrink ray
item_growth_ray = growth ray
item_chain = chain
item_time_lock = time lock
item_throw = throw
item_teleport = teleport
item_grappling_hook = grappling hook
item_drill = drill
item_beam = beam
```

## API

### Make a new translation
```cs
CustomLanguage english = BoplTranslator.GetCustomLanguage(Language.EN);
english.EditTranslation("com.almafa64.custom_gun_ability", "balloon gun");
// or english["com.almafa64.custom_gun_ability"] = "balloon gun";
```
**Note**: `BoplTranslator.GetCustomLanguage(Language language)` gets the `CustomLanguage` associated with `language`. Because of how enums work in c# this parameter can be bigger than `Language` last element (13), when this happens it gets a user made language.

### Get a translation
```cs
CustomLanguage english = BoplTranslator.GetCustomLanguage("en");
string customGunName = english.GetTranslation("com.almafa64.custom_gun_ability");
// or string customGunName = english["com.almafa64.custom_gun_ability"];
```
**Note**: You can use `BoplTranslator.GetCustomLanguage(string langName)` to get language by name.

### Make a new language
```cs
CustomLanguage myLanguage = new CustomLanguage("HU");

Dictionary<string, string> hunTranslations = new Dictionary<string, string>()
{
	{ "menu_exit", "Kilépés" },
	{ "screen_fullscreen", "Teljes képernyõ" }
};

myLanguage.EditTranslations(hunTranslations);
```
**Note**: `new CustomLanguage(string name, GameFont font)` copies fallback language (which was set in config) into itself, so you can edit every in-game text (see above for all in-game text) by default.

### Make a `TextMeshProUGUI` / `TextMesh` translatable
```cs
GameObject gun = GameObject.Find("Balloon Gun nameplate");
BoplTranslator.AttachLocalizedText(gun, "com.almafa64.custom_gun_ability", true);
```
**Note**: Before any call to `BoplTranslator.AttachLocalizedText` make sure you make all translations needed with `language.EditTranslation` (it won't break, but `EditTranslation` doesn't update `LocalizedText`s).
**Note2**: GameObject should already have `TextMeshProUGUI` or `TextMesh` component.

### Update LocalizedText
```cs
GameObject gun = GameObject.Find("Balloon Gun nameplate");
gun.GetComponent<LocalizedText>().UpdateText();
```

### Update all LocalizedText
```cs
BoplTranslator.UpdateTexts();
```