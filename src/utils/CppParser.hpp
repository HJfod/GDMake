#pragma once

#include "utils.hpp"
#include <string>
#include <vector>

enum CppParseError {
    OK = 0,
    FileDoesNotExist,
    CantReadFile,
};

class CppParser {
    public:
        enum CppFileType {
            Normal,
            Source,
            Header,
        };

        template<class T>
        using tk_ptr = std::shared_ptr<T>;

        struct Token {
            ext::string rawText;
            ext::string content = "";
            ext::vector<ext::string> namespaces;

            Token(ext::string const& raw) : rawText(raw) {}
            Token(ext::string const& raw, ext::vector<ext::string> const& ns)
                : rawText(raw), namespaces(ns) {}
            Token(ext::string const& raw, ext::vector<ext::string> const& ns, ext::string const& content)
                : rawText(raw), namespaces(ns), content(content) {}

            virtual ~Token() = default;
        };
        struct tkPre : public Token {
            inline tkPre(ext::string const& raw)
                : Token(raw) {}
            inline tkPre(ext::string const& raw, ext::vector<ext::string> const& ns)
                : Token(raw, ns) {}
        };
        struct tkInclude : public tkPre {
            ext::string includePath;
            bool global;

            inline tkInclude(ext::string const& raw, ext::string const& inc, bool g)
                : tkPre(raw), includePath(inc), global(g) {}
        };
        struct tkDefine : public tkPre {
            std::string definition;

            inline tkDefine(ext::string const& raw, std::string const& def)
                : tkPre(raw), definition(def) {}
        };
        struct tkUsing : public Token {
            inline tkUsing(ext::string const& raw, std::vector<ext::string> const& ns)
                : Token(raw, ns) {}
        };
        struct tkFunction : public Token {
            ext::string funcName;
            ext::string callConv;
            ext::string returnType;
            ext::string argString;
            ext::vector<ext::string> macros;

            inline tkFunction(
                ext::string const& raw,
                std::vector<ext::string> const& ns,
                ext::string const& fname,
                ext::string const& cconv,
                ext::string const& rtype,
                ext::string const& argstr,
                ext::vector<ext::string> const& mcrs
            ) : Token(raw, ns),
                funcName(fname),
                callConv(cconv),
                returnType(rtype),
                argString(argstr),
                macros(mcrs) {}
        };
        struct tkClass : public Token {
            ext::string className;
            bool isStruct;

            inline tkClass(
                ext::string const& raw,
                std::vector<ext::string> const& ns,
                ext::string const& name,
                bool strct
            ) : Token(raw, ns), className(name), isStruct(strct) {}
        };
    
    protected:
        std::string m_rawFile;
        std::vector<tk_ptr<Token>> m_tokens;

    public:
        std::vector<tk_ptr<Token>> & getTokens();
        std::vector<tk_ptr<Token>> getPreprocessorTokens();
        std::vector<tkUsing> getUsings();
        std::vector<tkFunction> getFunctions();
        std::vector<tkClass> getClasses();

        CppParser() = default;
        ~CppParser() = default;

        CppParseError parse(std::string const& file);
        std::string save(CppFileType);
};
