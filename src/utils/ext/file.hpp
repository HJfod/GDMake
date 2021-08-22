#pragma once

#include "string.hpp"
#include <filesystem>
#include <fstream>
#include "result.hpp"

namespace ext {
    template<class DataT>
    class file {
        public:
            using BinArray = std::vector<std::uint8_t>;

        protected:
            ext::string m_path;
            DataT m_data;

        public:
            file() = default;
            ~file() = default;
            inline file(ext::string const& path) {
                m_path = path;
            }

            file & load(ext::string const& path) {
                m_path = path;
                return *this;
            }

            bool exists() const {
                return std::filesystem::exists(m_path.asStd());
            }

            template<class SaveT = DataT> bool save();
            template<> inline bool save<ext::string>() {
                if (!m_path) return false;
                
                std::ofstream file;
                file.open(m_path);
                if (file.is_open()) {
                    file << m_data;
                    file.close();
                    return true;
                }
                file.close();
                return false;
            }
            template<> inline bool save<BinArray>() {
                if (!m_path) return false;

                std::ofstream file;
                file.open(m_path, std::ios::out | std::ios::binary);
                if (file.is_open()) {
                    file.write(reinterpret_cast<const char*>(m_data.data()), m_data.size());
                    file.close();
                    return true;
                }
                file.close();
                return false;
            }
            
            template<class ReadT = DataT> bool read();
            template<> inline bool read<ext::string>() {
                if (!m_path) return false;

                std::ifstream in(m_path, std::ios::in | std::ios::binary);
                if (in) {
                    std::string contents;

                    in.seekg(0, std::ios::end);
                    contents.resize((const size_t)in.tellg());
                    in.seekg(0, std::ios::beg);
                    in.read(&contents[0], contents.size());
                    in.close();

                    m_data = contents;

                    return true;
                }
                return false;
            }
            template<> inline bool read<BinArray>() {
                std::ifstream in(m_path, std::ios::in | std::ios::binary);
                if (in) {
                    m_data = BinArray ( std::istreambuf_iterator<char>(in), {} );
                    return true;
                }
                return false;
            }
            
            inline DataT data() { return m_data; }
            inline file & data(DataT dat) { m_data = dat; return *this; }

            inline std::filesystem::path path() {
                return std::filesystem::path(m_path.asStd());
            }
            inline ext::string path_string() {
                return m_path;
            }
    };

    class dir {
        public:
            using iterate_res = ext::result<ext::vector<ext::string>>;

            inline static iterate_res iterate_dir(
                ext::string const& path, ext::vector<ext::string> const& exts = {}
            ) {
                if (!std::filesystem::exists(path.asStd()))
                    return iterate_res::err("File does not exist");
                
                ext::vector<ext::string> res;

                for (auto f : std::filesystem::recursive_directory_iterator(path.asStd()))
                    if (f.is_regular_file()) {
                        if (exts.size()) {
                            if (exts.contains(f.path().extension().string()))
                                res.push_back(f.path().string());
                        } else
                            res.push_back(f.path().string());
                    }
                
                return iterate_res::res(res);
            }
    };
}
