# Windowsshell  
### (Timber’s Windows Shell)

A lightweight, slightly chaotic Windows shell replacement / overlay UI experiment.

It is not polished. It is not stable. It *does things*.

---

## ⚠️ Disclaimer

This project was built more as a UI experiment than a production shell replacement.

Expect:
- weird behavior
- hidden shortcuts
- things that only work if you click the “right wrong” spot
- hardcoded shortcuts that may or may not make sense

---

## 🚀 Features

### 🧭 Desktop Overlay UI
A custom shell-style interface layered over Windows with:

- Taskbar-like bottom panel
- Clickable system widgets
- Hidden interaction toggles
- Custom app shortcuts (some hardcoded)

---

## ⌨️ Hidden / Special Controls

Some elements are intentionally not obvious:

### 🖱️ Interaction Unlock
- `Ctrl + Shift + Caps Lock` → toggles clickability for certain UI elements

### 🕒 Time Widget
- Clicking the **clock/time** toggles or enables extra interactions (debug-style unlock behavior)

### 🌡️ Temperature Widget
- Clicking temperature:
  - toggles weather display OFF for privacy mode

### 💬 Discord Icon
- Does NOT directly open Discord by default
- Requires configuration (see below)

---

## ⚙️ Settings (Top Left Gear Icon)

Clicking the settings icon opens the configuration panel.

### Available Settings:

#### 💬 Discord Channels
- Format:
  ```
  "<id>, <id>, <id>"
  ```
- Comma-separated Discord channel IDs used for integrations or widgets

#### 🌍 Weather Location
- Set:
  - Latitude
  - Longitude
- Used for weather widget location targeting

#### 🕶️ Private Username Mode
- Enables censorship of console/log output for a given username
- Used for demo-safe recordings

#### 🧩 Discord Path
Default:
```
%localappdata%\discord\Update.exe
```

- Can be changed to any executable path
- Used for launching Discord via the shell shortcut system

---

## 🧠 Known Hardcoded Behavior

- Discord launcher icon is not guaranteed to work unless configured correctly
- Some integrations assume Windows environment variables exist
- Termius is currently hardcoded into launcher behavior
  - You can replace it by changing:
    - icon
    - executable path
    - launcher binding

---

## 🛠️ Compilation (??)

Honestly:

> I don’t fully know how you’re compiling this either.

But in theory, it depends on what this evolved from:

- If it’s **C# / WPF / WinUI**:
  ```
  dotnet build
  ```

- If it’s **C++ shell replacement**:
  - Visual Studio build
  - target x64 Release
  - pray

- If it’s **Blazor / WebView shell**:
  ```
  dotnet publish -c Release
  ```

- If it’s **pure chaos JS overlay**:
  - npm install (maybe)
  - npm run build (maybe)

---

## 🧪 Philosophy

Windowsshell is not meant to replace Windows Explorer in a serious way.

It is:
- a sandbox UI layer
- a customizable shell experiment
- a “what if Windows but I controlled everything” project

---

## 🔧 Tips

- If something doesn’t respond → try unlocking click mode:
  ```
  Ctrl + Shift + Caps Lock
  ```
- If Discord doesn’t open → check the path in settings
- If weather seems wrong → verify lat/long
- If something feels hardcoded → it probably is

---

## 🧷 Future Ideas (maybe)

- proper plugin system
- draggable widgets
- real taskbar process binding
- configurable hotkey system
- less hardcoding (optional)
- more chaos (definitely optional)

---

## 🪟 Closing Note

This shell is basically:
> “Windows, but I wanted to see what breaks if I touch everything.”

Use it, break it, rebuild it, or replace it.

Good luck.
```
