#include "../utils/utils.hpp"

#define GDMAKE_HOOK(...)
#define GDMAKE_PROP()

namespace nest {
    namespace cool {
        namespace epic {
            void func();
        }

        void epic  :: func() {

        }

        static inline GDMAKE_PROP() bool GDMAKE_HOOK (epic stuff) __fastcall anotherFunc() {
            
        }
    }
}
