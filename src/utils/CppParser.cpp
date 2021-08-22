#include "CppParser.hpp"
#include <iostream>
#include "Console.hpp"
#include <map>

// beauty
#if defined(DEBUG) || defined(MORE_DEBUG)
    #define CPP_DEBUG(...) __VA_ARGS__
    #ifdef MORE_DEBUG
        #define CPP_DEBUG_DUMP_DATA(...) __VA_ARGS__
    #else
        #define CPP_DEBUG_DUMP_DATA(...)
    #endif
#else
    #define CPP_DEBUG(...)
    #define CPP_DEBUG_DUMP_DATA(...)
#endif

#define TK_IS(type) \
    tk##type* p##type; \
    if ((p##type = dynamic_cast<tk##type*>(token.get())) && (cast = true))

void removeComments(ext::string & str) {
    str += "\n";

    ext::string copy = str;
    str = "";
    
    char last_char = 0;
    enum : unsigned char { i_none, i_single, i_double, } in = i_none;
    enum : unsigned char { r_none, r_line, r_block, } removing = r_none;
    for (auto & c : copy) {
        if (last_char) {
            // char literals aren't large enough to hold
            // comments
            if (removing == r_none) {
                if (c == '\\' && last_char == '\\') {
                    last_char = 1;
                    str += c;
                    continue;
                }

                if (c == '\'' && last_char != '\\')
                    if (in == i_none) in = i_single;
                    else if (in == i_single) in = i_none;
                
                if (c == '"' && last_char != '\\')
                    if (in == i_none) in = i_double;
                    else if (in == i_double) in = i_none;
            }

            switch (last_char) {
                case '/':
                    if (in == i_none) {
                        if (c == '/') {
                            removing = r_line;
                            str.pop_back();
                            break;
                        }
                        else if (c == '*') {
                            removing = r_block;
                            str.pop_back();
                            break;
                        }
                    } goto default_label; // because for some god knows
                                          // reason goto default is disallowed

                case '*':
                    if (in == i_none)
                        if (c == '/' && removing == r_block) {
                            removing = r_none;
                            break;
                        }
                    goto default_label;

                case '\n':
                    if (removing == r_line)
                        removing = r_none;
                
                default_label:
                default:
                    if (removing == r_none)
                        str += c;
            }
        } else
            str += c;

        last_char = c;
    }
}

enum StringLiteralState : unsigned char { r_none = 0, r_single, r_double, };
StringLiteralState inStringLiteral(ext::string const& str, StringLiteralState last) {
    StringLiteralState in = last;
    char last_char = 0;
    for (auto & c : str) {
        switch (c) {
            case '\\':
                if (last_char == '\\') {
                    last_char = 0;
                    continue;
                }
                break;

            case '\'':
                if (last_char != '\\') {
                    if (in == r_single)
                        in = r_none;
                    else if (in == r_none)
                        in = r_single;
                }
                break;
            
            case '"':
                if (last_char != '\\') {
                    if (in == r_double)
                        in = r_none;
                    else if (in == r_none)
                        in = r_double;
                }
                break;
        }

        last_char = c;
    }
    return in;
}

CppParseError CppParser::parse(std::string const& path) {
    auto file = ext::file(path);

    if (!file.exists())
        return FileDoesNotExist;

    if (file.read()) {
        CPP_DEBUG(
            auto io = io::Console::get();

            io << io::Color::Cyan << "Parsing " << path << io::Color::White << "\n";
        )

        auto data = file.data();
        this->m_rawFile = data;

        size_t bracketCount = 0u;

        removeComments(data);

        // remove windows line endings
        data.replace_self("\r\n", '\n');

        ext::string collectDefine = "";

        auto line_ix = 0u;
        // collect defines and includes
        for (auto rline : data.split('\n')) {
            line_ix++;

            auto line = rline.normalize().trim();

            if (collectDefine.size()) {
                collectDefine += rline + "\n";
                data.remove_line(--line_ix);

                if (!line.endsWith('\\')) {
                    this->m_tokens.push_back(std::make_shared<tkDefine>(tkDefine { rline, collectDefine }));

                    collectDefine = "";
                }
            }

            if (line.startsWith('#')) {
                line = line.substr(1).trim_left();

                if (line.startsWith("include"))
                    if (line.contains('"')) {
                        line = line.substr(line.indexOf('"') + 1).substr_s(0, '"');

                        this->m_tokens.push_back(std::make_shared<tkInclude>(tkInclude { rline, line.trim(), false }));
                    } else {
                        line = line.substr(line.indexOf('<') + 1).substr_s(0, '>');

                        this->m_tokens.push_back(std::make_shared<tkInclude>(tkInclude { rline, line.trim(), true }));
                    }
                else if (line.startsWith("define")) {
                    if (line.endsWith('\\')) {
                        collectDefine += line.substr_s(' ').trim() + "\n";
                    } else {
                        this->m_tokens.push_back(std::make_shared<tkDefine>(tkDefine { rline, line.substr_s(' ').trim() }));
                    }
                } else
                    this->m_tokens.push_back(std::make_shared<tkPre>(tkPre { rline }));

                data.remove_line(--line_ix);
                
                continue;
            }
        }

        data.replace_self('\n', ' ');

        bracketCount = 0u;
        bool notInANamespace = false;
        StringLiteralState wellBoysWeAreInAComment = r_none;
        ext::stack<size_t, ext::string> namespaces;

        for (auto [piece, sChar] : data.split_a({ ';', '{', '}' })) {
            piece.trim_self();

            if (!notInANamespace) {
                auto first = piece.substr_s(0, ' ');

                if (piece.contains('<'))
                    if (piece.indexOf('<') < piece.indexOf(' '))
                        first = piece.substr_s(0, '<');

                switch (first) {
                    case h$("using"): {
                        piece.normalize_self();
                        this->m_tokens.push_back(std::make_shared<tkUsing>(tkUsing { piece, namespaces.get_column2() }));
                    } break;


                    case h$("namespace"): {
                        piece.normalize_self();
                        auto ns = piece.substr_s(' ');

                        if (ns.contains("::"))
                            for (auto ns_ : ns.split("::"))
                                namespaces.push(bracketCount + 1, ns_.trim());
                        else
                            namespaces.push(bracketCount + 1, ns.trim());
                    } break;

                    case h$("template"):
                        if (piece.contains("class") || piece.contains("struct"))

                    case h$("class"): case h$("struct"): {
                        piece.normalize_self();
                        bool isStruct = piece.contains("struct ");
                        piece.replace_self("struct ", "class ");
                        if (piece.contains(':'))
                            this->m_tokens.push_back(std::make_shared<tkClass>(tkClass {
                                piece, namespaces.get_column2(),
                                piece.substr_s(piece.lastIndexOf("class ") + 6, ':').trim(), isStruct
                            }));
                        else
                            this->m_tokens.push_back(std::make_shared<tkClass>(tkClass {
                                piece, namespaces.get_column2(),
                                piece.substr_s(piece.lastIndexOf("class ") + 6, ' ').trim(), isStruct
                            }));
                    }
                        else    // i hate this

                    default:
                        if (sChar == '{' && piece.contains('(')) {
                            piece.trim_self()->normalize_self();

                            size_t iParen = 0u;
                            size_t lParen = 0u;

                            size_t ix = 0u;
                            for (auto const& c : piece) {
                                switch (c) {
                                    case '(':
                                        if (!iParen) lParen = ix;
                                        iParen++;
                                        break;
                                    case ')':
                                        if (iParen > 0) iParen--;
                                        break;
                                }
                                ix++;
                            }

                            auto retAndName = piece.substr(0, lParen).trim();
                            auto args = piece.substr(lParen).trim();

                            retAndName.replace_self(":: ", "::");
                            retAndName.replace_self(" ::", "::");
                            retAndName.replace_self(" (", "(");

                            auto name = retAndName.substr(retAndName.lastIndexOf(' ') + 1);
                            retAndName = retAndName.substr(0, retAndName.lastIndexOf(' '));

                            auto ns = namespaces.get_column2();
                            if (name.contains("::")) {
                                auto name_ns = name.substr(0, name.lastIndexOf("::")).trim();
                                name = name.substr(name.lastIndexOf("::") + 2);

                                if (name_ns.contains("::"))
                                    ns.push_sub(name_ns.split("::"));
                                else
                                    ns.push_back(name_ns.trim());
                            }

                            ext::vector<ext::string> macros;
                            ext::string retType = "";
                            while (retAndName.contains('(')) {
                                auto macroArgs = retAndName.substr_s('(');
                                macroArgs = macroArgs.substr(0, macroArgs.indexOf(')') + 1);
                                auto macroName = retAndName.substr_s(0, '(');
                                size_t off = 0u;
                                if (macroName.contains(' ')) {
                                    off = macroName.lastIndexOf(' ') + 1;
                                    macroName = macroName.substr(off);
                                }
                                macros.push_back(macroName + macroArgs);
                                retAndName.erase(
                                    retAndName.begin() + off,
                                    retAndName.begin() + retAndName.indexOf(')') + 1
                                );
                                retAndName.trim_self();
                            }

                            ext::string cconv = "";
                            for (auto const& tk : retAndName.split(' '))
                                if (tk.contains(ext::vector<ext::string> {
                                    "thiscall",
                                    "fastcall",
                                    "stdcall",
                                    "cdecl",
                                    "vectorcall"
                                }))
                                    cconv = tk;
                                else
                                    retType += tk + ' ';
                            
                            retType.normalize_self()->trim_self();

                            this->m_tokens.push_back(std::make_shared<tkFunction>(tkFunction {
                                piece, ns, name, cconv, retType, args, macros
                            }));
                        } else {
                            this->m_tokens.push_back(std::make_shared<Token>(Token {
                                piece, namespaces.get_column2()
                            }));
                        }
                }
            } else {
                auto bcount = bracketCount - namespaces.size();
                this->m_tokens.at(this->m_tokens.size() - 1).get()->content +=
                    std::string((bcount - (sChar == '}')) * 4, ' ') +
                    piece.normalize().trim() + ((bcount == 1 && sChar == '}') ? ' ' : sChar) + '\n';
            }

            wellBoysWeAreInAComment = inStringLiteral(piece, wellBoysWeAreInAComment);
            if (wellBoysWeAreInAComment)
                continue;
            
            switch (sChar) {
                case '{':
                    bracketCount++;

                    if (!namespaces.size() || namespaces.value1() != bracketCount)
                        notInANamespace = true;
                    break;

                case '}':
                    auto token = this->m_tokens.at(this->m_tokens.size() - 1).get();
                    if (namespaces.size() && namespaces.value1() == bracketCount)
                        namespaces.pop();

                    if (bracketCount > 0) bracketCount--;

                    if (bracketCount == 0)
                        notInANamespace = false;

                    if (namespaces.size() && namespaces.value1() == bracketCount)
                        notInANamespace = false;
                    break;
            }
        }

        CPP_DEBUG(
            for (auto & tk : m_tokens) {
                if (dynamic_cast<tkInclude*>(tk.get()))
                    io << "include: " << io::Green
                        << dynamic_cast<tkInclude*>(tk.get())->includePath << " "
                        << io::Pink << dynamic_cast<tkInclude*>(tk.get())->global
                        << io::Gray << " -> ";
                else if (dynamic_cast<tkDefine*>(tk.get()))
                    io << "define: " << io::Cyan << dynamic_cast<tkDefine*>(tk.get())->definition << io::Gray << " -> ";
                else if (dynamic_cast<tkUsing*>(tk.get()))
                    io << "using: " << io::Purple << dynamic_cast<tkUsing*>(tk.get())->rawText << io::Gray << " -> ";
                else if (dynamic_cast<tkClass*>(tk.get()))
                    io << "class: "
                        << io::Blue << dynamic_cast<tkClass*>(tk.get())->namespaces.join("::") << "::"
                        << io::Red << dynamic_cast<tkClass*>(tk.get())->className << ' '
                        << io::Pink << dynamic_cast<tkClass*>(tk.get())->isStruct
                        << io::Gray << " -> ";
                else if (dynamic_cast<tkFunction*>(tk.get()))
                    io
                        << "function: "
                        << io::Cyan << dynamic_cast<tkFunction*>(tk.get())->macros.join(", ") << ' '
                        << io::Lime << dynamic_cast<tkFunction*>(tk.get())->returnType << ' '
                        << io::Red << dynamic_cast<tkFunction*>(tk.get())->callConv << ' '
                        << io::Blue << dynamic_cast<tkFunction*>(tk.get())->namespaces.join("::") << "::"
                        << io::Pink << dynamic_cast<tkFunction*>(tk.get())->funcName
                        << io::Yellow << dynamic_cast<tkFunction*>(tk.get())->argString
                        << io::Gray << " -> ";
                else
                    io << "other: " << io::Lemon << tk.get()->rawText << io::Gray << " -> ";
                
                io << io::Red << tk.get()->content.size() << io::White << "\n";

                CPP_DEBUG_DUMP_DATA(
                    io << io::Gray << tk.get()->content << io::White << "\n";
                )
            }

            std::cout << "==============\n\n";
        )

        return OK;
    } else
        return CantReadFile;
}

std::vector<CppParser::tkClass> CppParser::getClasses() {
    std::vector<tkClass> res;
    for (auto const& tk : this->m_tokens)
        if (dynamic_cast<tkClass*>(tk.get()))
            res.push_back(*dynamic_cast<tkClass*>(tk.get()));
    return res;
}

std::vector<CppParser::tkUsing> CppParser::getUsings() {
    std::vector<tkUsing> res;
    for (auto const& tk : this->m_tokens)
        if (dynamic_cast<tkUsing*>(tk.get()))
            res.push_back(*dynamic_cast<tkUsing*>(tk.get()));
    return res;
}

std::vector<CppParser::tkFunction> CppParser::getFunctions() {
    std::vector<tkFunction> res;
    for (auto const& tk : this->m_tokens)
        if (dynamic_cast<tkFunction*>(tk.get()))
            res.push_back(*dynamic_cast<tkFunction*>(tk.get()));
    return res;
}

std::vector<CppParser::tk_ptr<CppParser::Token>> CppParser::getPreprocessorTokens() {
    std::vector<tk_ptr<Token>> res;
    for (auto const& tk : this->m_tokens)
        if (dynamic_cast<tkPre*>(tk.get()))
            res.push_back(tk);
    return res;
}

std::vector<CppParser::tk_ptr<CppParser::Token>> & CppParser::getTokens() {
    return this->m_tokens;
}

std::string CppParser::save(CppParser::CppFileType type) {
    std::string res;

    for (auto const& token : m_tokens) {
        bool cast = false;
        TK_IS(Include) {

        }
        TK_IS(Define) {

        }
        if (!cast) {
            
        }
    }

    return res;
}
