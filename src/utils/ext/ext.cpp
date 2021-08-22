#include "ext.hpp"

ext::string::split_list ext::string::split_a(ext::vector<char> const& splitcs) const {
    split_list res;

    ext::string collect = "";
    for (auto c : *this)
        if (splitcs.contains(c)) {
            res.push_back({ collect, c });
            collect = "";
        } else collect += c;
    
    if (collect)
        res.push_back({ collect, 0 });

    return res;
}

ext::vector<ext::string> ext::string::split(ext::string const& split) const {
    ext::vector<ext::string> res;

    if (this->size()) {
        auto s = *this;

        size_t pos = 0;

        while ((pos = s.find(split)) != std::string::npos) {
            res.push_back(s.substr(0, pos));
            s.erase(0, pos + split.length());
        }
        if (s.size())
            res.push_back(s);
    }

    return res;
}
