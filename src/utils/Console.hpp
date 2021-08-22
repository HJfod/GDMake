#pragma once

#include <iostream>
#include <vector>
#include <Windows.h>
#include <functional>
#include "ext/ext.hpp"

using uint = unsigned int;
using coord = short;

namespace io {
enum Color {
    White,
    Black,
    Red,
    Green,
    Blue,
    Yellow,
    Purple,
    Gray,
    Light,
    Lime,
    Lemon,
    Pink,
    Cyan,
};

struct BG {
    Color color;

    inline BG() {};
    inline BG(Color const& _col) : color(_col) {};
};

class Console;

class Line {
    protected:
        coord pos;
        std::string content;
        Console* parent;
    
    public:
        Line* setContent(std::string const&);
        std::string getContent();

        Line(Console*);

        friend class Console;
};

class ProgressBar : protected Line {

};

class Console {
    public:
        enum IOSpecialOut {
            Await
        };

        enum Charset {
            ASCII,
            ANSI,
            __UNICODE,
            UTF8
        };

    protected:
        std::istream & inStream;
        std::ostream & outStream;

        Color current;
        BG currentBG;

        HANDLE handle;
        WORD prevColor;

        WORD getColor(Color);
        WORD getColor(BG);

        coord getCurrentLine();
        COORD getPos();
        void updateLine(Line*);
        uint cline = 0;

        std::vector<Line*> lines;

        friend class Line;

    public:
        Console(std::istream &, std::ostream &);
        ~Console();

        static Console & initShared(std::istream &, std::ostream &);
        static Console & get();

        Console * setCharset(Charset);

        template<typename T>
        inline Console & operator<< (T _out) {
            this->outStream << _out;

            return *this;
        }
        Console & operator<< (std::string const&);
        Console & operator<< (ext::string const&);
        Console & operator<< (const char*);
        Console & operator<< (Color);
        Console & operator<< (BG const&);
        Console & operator<< (IOSpecialOut);

        template<typename T>
        Console & operator>> (T);

        Console * await(std::string const& = "Press any key to continue ");
        Console * out(std::string const&);
        Console * fg(Color);
        Console * bg(Color);
        Console * nl(int = 1);
        Console * clear();

        std::string in();

        Console & showLoad(bool * stop);

        Line* line(std::string const& = "");
        uint selectMenu(std::vector<std::string> const&, bool = false);
        Console * progressBar(std::function<void(ProgressBar*)>);
};
}
