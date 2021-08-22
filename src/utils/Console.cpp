#include "Console.hpp"
#include <conio.h>
#include <string>
#include <map>
#include <algorithm>
#include <thread>

namespace io {

Console* g_console;

std::string Line::getContent() {
    return this->content;
}

Line* Line::setContent(std::string const& _txt) {
    this->content = _txt;
    this->parent->updateLine(this);

    return this;
}

Line::Line(Console* _con) {
    this->parent = _con;

    this->pos = _con->getCurrentLine();
}



Console::Console(std::istream & _in, std::ostream & _out)
    : inStream(_in), outStream(_out) {
        this->handle = GetStdHandle(STD_OUTPUT_HANDLE);

        this->currentBG.color = Color::Black;
        this->current = Color::White;

        SetConsoleOutputCP(CP_UTF8);
    }

Console::~Console() {
    // set color back
    SetConsoleTextAttribute(
        this->handle,
        FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE
    );
}

std::map<Color, char> colors {
    { White,        0b1110 },
    { Black,        0b0000 },
    { Red,          0b1000 },
    { Green,        0b0100 },
    { Blue,         0b0010 },
    { Yellow,       0b1100 },
    { Purple,       0b1010 },
    { Gray,         0b0001 },
    { Light,        0b1111 },
    { Lime,         0b0101 },
    { Lemon,        0b1101 },
    { Pink,         0b1011 },
    { Cyan,         0b0111 },
};

WORD generateFGColor(char c) {
    WORD res = 0;

    if (c & 0b1000) res |= FOREGROUND_RED;
    if (c & 0b0100) res |= FOREGROUND_GREEN;
    if (c & 0b0010) res |= FOREGROUND_BLUE;
    if (c & 0b0001) res |= FOREGROUND_INTENSITY;

    return res;
}

WORD generateBGColor(char c) {
    WORD res = 0;

    if (c & 0b1000) res |= BACKGROUND_RED;
    if (c & 0b0100) res |= BACKGROUND_GREEN;
    if (c & 0b0010) res |= BACKGROUND_BLUE;
    if (c & 0b0001) res |= BACKGROUND_INTENSITY;

    return res;
}

WORD Console::getColor(Color _col) {
    return generateFGColor(colors[_col]);
}

WORD Console::getColor(BG _col) {
    return generateBGColor(colors[_col.color]);
}

Console * Console::setCharset(Charset _cs) {
    switch (_cs) {
        default:
        case Charset::ASCII:    break;
        case Charset::ANSI:     SetConsoleOutputCP(CP_WINANSI); break;
        case Charset::UTF8:     SetConsoleOutputCP(CP_UTF8); break;
        case Charset::__UNICODE:SetConsoleOutputCP(CP_WINUNICODE); break;
    }

    return this;
}


Console * Console::await(std::string const& _str) {
    this->outStream << _str;
    _getch();
    this->outStream << "\n";

    return this;
}

Console * Console::out(std::string const& _str) {
    this->outStream << _str;

    return this;
}

Console * Console::fg(Color _col) {
    *this << _col;

    return this;
}

Console * Console::bg(Color _col) {
    *this << BG(_col);

    return this;
}

Console * Console::nl(int _c) {
    this->cline += _c;

    while (_c--)
        this->outStream << "\n";
    
    return this;
}

Console * Console::clear() {
    return this->bg(Color::Black)->fg(Color::White);
}

std::string Console::in() {
    std::string buff;
    this->inStream >> buff;
    return buff;
}


Console & Console::operator<< (const char* _out) {
    this->outStream << _out;

    std::string out(_out);

    return *this;
}

Console & Console::operator<< (ext::string const& _out) {
    this->outStream << _out;

    return *this;
}

Console & Console::operator<< (std::string const& _out) {
    this->outStream << _out;

    return *this;
}

Console & Console::operator<< (Color _col) {
    CONSOLE_SCREEN_BUFFER_INFO info;
    GetConsoleScreenBufferInfo(this->handle, &info);
    this->prevColor = info.wAttributes;

    this->current = _col;

    SetConsoleTextAttribute(this->handle, getColor(this->current) | getColor(this->currentBG));

    return *this;
}

Console & Console::operator<< (BG const& _col) {
    CONSOLE_SCREEN_BUFFER_INFO info;
    GetConsoleScreenBufferInfo(this->handle, &info);
    this->prevColor = info.wAttributes;

    this->currentBG = _col;

    SetConsoleTextAttribute(this->handle, getColor(this->current) | getColor(this->currentBG));

    return *this;
}

Console & Console::operator<< (Console::IOSpecialOut _spec) {
    switch (_spec) {
        case IOSpecialOut::Await:
            this->await(); break;
    }

    return *this;
}

template<typename T>
Console & Console::operator>> (T _in) {
    this->inStream >> _in;

    return *this;
}


uint Console::selectMenu(std::vector<std::string> const& _items, bool _ins) {
    this->bg(Color::Black)->fg(Color::White);

    if (_ins)
        this->out("<Use arrow keys to navigate, Enter to select>")->nl();

    size_t l = 0;
    for (auto const& item : _items)
        if (item.length() > l)
            l = item.length();
    
    l += 6;

    auto render = [&_items, this, l](uint selected) -> void {
        auto i = 0u;
        for (auto const& item : _items) {
            auto str = "[" + std::to_string(++i) + "] " + item;

            this->fg(selected == i - 1 ? Color::Lime  : Color::Gray);
            this->bg(selected == i - 1 ? Color::Black : Color::Black);

            *this << str << BG(Color::Black) << std::string(l - str.length(), ' ');
        }
    };
    render(0u);

    auto selected = 0u;
    int ch;
    while ((ch =_getch()) != '\r' && ch != ' ' && (ch < 48 || ch > 48 + _items.size())) {
        switch (ch) {
            case 'K': case 'a': case 'A': case 'D':
                if (selected)
                    selected--;
                else selected = (unsigned int)_items.size() - 1;
                break;

            case 'M': case 'd': case 'C': case 'c':
                if (selected < _items.size() - 1)
                    selected++;
                else selected = 0;
                break;
        }
        *this << std::string(l * _items.size(), '\r');
        render(selected);
    }
    
    this->clear()->nl();

    if (ch >= 48 && ch <= 57)
        selected = ch - 49;

    return selected;
}

coord Console::getCurrentLine() {
    // CONSOLE_SCREEN_BUFFER_INFO cbsi;
    // GetConsoleScreenBufferInfo(this->handle, &cbsi);

    // return cbsi.dwCursorPosition.Y;
    return this->cline;
}

COORD Console::getPos() {
    CONSOLE_SCREEN_BUFFER_INFO cbsi;
    GetConsoleScreenBufferInfo(this->handle, &cbsi);

    return cbsi.dwCursorPosition;
}

void Console::updateLine(Line* _l) {
    auto pos = this->getPos();

    SetConsoleCursorPosition(this->handle, { 2, 8 });

    this->outStream << _l->content << this->cline << " " << _l->pos;

    SetConsoleCursorPosition(this->handle, pos);
}

Line* Console::line(std::string const& _txt) {
    auto ret = new Line(this);

    ret->content = _txt;
    this->outStream << _txt << "\n";
    this->cline++;

    return ret;
}


Console & Console::initShared(std::istream & _in, std::ostream & _out) {
    g_console = new Console(_in, _out);

    return *g_console;
}

Console & Console::get() {
    return *g_console;
}

Console & Console::showLoad(bool * stop) {
    this->outStream << '-' << std::flush;

    std::thread t([this, stop]() -> void {
        static constexpr const int animation_speed = 200;
        auto ix = 0;
        std::vector<const char*> texts { "\b\\", "\b|", "\b/", "\b-" };

        while (!*stop) {
            ix++;
            if (ix >= texts.size()) ix = 0;

            this->outStream << texts[ix] << std::flush;
            std::this_thread::sleep_for(std::chrono::milliseconds(animation_speed));
        }
    });

    t.detach();

    return *this;
}

}
