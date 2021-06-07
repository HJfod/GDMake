# GDMake

### A tool for creating GD mods

---

GDMake is a tool meant for simplifying and speeding up the development of Geometry Dash mods.

---

# Example

This simple mod will add a new text to the main menu.

```cpp
// Include GDMake macros, helpers, submodules
#include <GDMake.h>

// Create a hook for the main menu;
// One could also do MenuLayer::init
// instead of the address, however
// to hook with the name instead of
// the address requires the address
// to be defined in the user dictionary

// The address is relative to GD base
// address
GDMAKE_HOOK(0x1907b0)
bool __fastcall MenuLayer_init(gd::MenuLayer* self) {
    // Call original function (leaving this out
    // would result in overwriting the original)
    // which we do not want rn

    // This will get replaced with a function call
    // when GDMake preprocesses the source files

    // The original returns a bool, which is false
    // if the initializing failed; if it failed, no
    // point in trying to add our own stuff to the
    // layer as it will be deleted shortly afterward
    if (!GDMAKE_ORIG(self))
        return false;
    
    // Get screen size
    auto winSize = cocos2d::CCDirector::sharedDirector()->getWinSize();
    // Create a new label with the text 'Hello world!'
    auto label = cocos2d::CCLabelBMFont::create("Hello world!", "bigFont.fnt");

    // Set label position to center of the screen.
    // This is equivalent to
    // label->setPosition(winSize.width / 2, winSize.height / 2)
    label->setPosition(winSize / 2);

    // Add the label to the main menu
    self->addChild(label, 100);
    
    // Init functions always return true if
    // initialization was succesful
    return true;
}
```

# Installation

1. `git clone`
2. `dotnet run`
3. Add `bin/Debug/net5.0/win10-x64` to the Path environment variable

# Usage

Basic usage:

 * `gdmake setup` for setting up GDMake
 * `gdmake init` for setting up a project
 * `gdmake .` for compiling & running

Use `gdmake help` for further information.

# Contribution

If you'd like to contribute, open a Pull Request.
