#include "cidn_loader.h"
#include <string.h>
#include <stdio.h>

#ifdef _WIN32
  #define WIN32_LEAN_AND_MEAN
  #include <windows.h>
  typedef HMODULE lib_handle_t;
  #define lib_open(p)      LoadLibraryW(p)
  #define lib_sym(h, n)    GetProcAddress(h, n)
  #define lib_close(h)     FreeLibrary(h)
  static lib_handle_t open_utf8(const char* path) {
      int wlen = MultiByteToWideChar(CP_UTF8, 0, path, -1, NULL, 0);
      wchar_t* wpath = (wchar_t*)_alloca(wlen * sizeof(wchar_t));
      MultiByteToWideChar(CP_UTF8, 0, path, -1, wpath, wlen);
      return LoadLibraryW(wpath);
  }
#else
  #include <dlfcn.h>
  typedef void* lib_handle_t;
  #define lib_open(p)      dlopen(p, RTLD_NOW | RTLD_LOCAL)
  #define lib_sym(h, n)    dlsym(h, n)
  #define lib_close(h)     dlclose(h)
  static lib_handle_t open_utf8(const char* path) { return lib_open(path); }
#endif

/* Thread-local last error */
#ifdef _WIN32
  static __declspec(thread) char tl_error[512];
#else
  static __thread char tl_error[512];
#endif

static void set_error(const char* msg) {
    strncpy(tl_error, msg, sizeof(tl_error) - 1);
    tl_error[sizeof(tl_error) - 1] = '\0';
}

const char* cidn_loader_last_error(void) { return tl_error; }

/* Module-level state */
static lib_handle_t g_lib = NULL;
static cidn_sym_table_t g_syms;
static int g_loaded = 0;

/* Macro to resolve one symbol; jumps to `fail` label on missing symbol */
#define RESOLVE(field, name) \
    do { \
        void* _sym = (void*)lib_sym(g_lib, name); \
        if (!_sym) { \
            snprintf(tl_error, sizeof(tl_error), \
                     "cidn_loader: symbol not found: %s (wrong library?)", name); \
            goto fail; \
        } \
        memcpy(&g_syms.field, &_sym, sizeof(void*)); \
    } while (0)

int cidn_loader_load(const char* libcurl_path, cidn_sym_table_t* table) {
    if (g_loaded) {
        if (table) *table = g_syms;
        return 0;
    }

    memset(&g_syms, 0, sizeof(g_syms));
    tl_error[0] = '\0';

    g_lib = open_utf8(libcurl_path);
    if (!g_lib) {
#ifdef _WIN32
        snprintf(tl_error, sizeof(tl_error),
                 "cidn_loader: LoadLibraryW failed for '%s' (error %lu)",
                 libcurl_path, (unsigned long)GetLastError());
#else
        snprintf(tl_error, sizeof(tl_error),
                 "cidn_loader: dlopen failed for '%s': %s",
                 libcurl_path, dlerror());
#endif
        return -1;
    }

    RESOLVE(global_init,     "curl_global_init");
    RESOLVE(global_cleanup,  "curl_global_cleanup");
    RESOLVE(easy_init,       "curl_easy_init");
    RESOLVE(easy_setopt_raw, "curl_easy_setopt");
    RESOLVE(easy_perform,    "curl_easy_perform");
    RESOLVE(easy_getinfo_raw,"curl_easy_getinfo");
    RESOLVE(easy_cleanup,    "curl_easy_cleanup");
    RESOLVE(easy_reset,      "curl_easy_reset");
    RESOLVE(easy_strerror,   "curl_easy_strerror");
    RESOLVE(slist_append,    "curl_slist_append");
    RESOLVE(slist_free_all,  "curl_slist_free_all");
    RESOLVE(share_init,      "curl_share_init");
    RESOLVE(share_setopt_raw,"curl_share_setopt");
    RESOLVE(share_cleanup,   "curl_share_cleanup");
    RESOLVE(share_strerror,  "curl_share_strerror");
    RESOLVE(curl_free,       "curl_free");
    RESOLVE(easy_impersonate,"curl_easy_impersonate");

    g_loaded = 1;
    if (table) *table = g_syms;
    return 0;

fail:
    lib_close(g_lib);
    g_lib = NULL;
    return -1;
}

void cidn_loader_unload(void) {
    /* Intentionally do NOT dlclose/FreeLibrary the upstream library. curl-impersonate links
     * BoringSSL, whose library destructors crash when the module is unloaded after TLS has been
     * used (SSL session cache + thread-locals are torn down on dlclose). The handle is left mapped
     * for the lifetime of the process and reclaimed by the OS at exit; we only drop our references
     * so the library can be re-resolved if cidn_global_init is called again. */
    g_lib = NULL;
    g_loaded = 0;
    memset(&g_syms, 0, sizeof(g_syms));
}

const cidn_sym_table_t* cidn_loader_symbols(void) {
    return g_loaded ? &g_syms : NULL;
}
