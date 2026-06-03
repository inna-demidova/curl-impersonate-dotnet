#pragma once

/* Vendored curl headers are compiled with -DCURL_STATICLIB so CURL_EXTERN
 * expands to nothing. We use them only for enums/typedefs/structs.
 * All actual function calls go through resolved function pointers below. */
#ifndef CURL_STATICLIB
#define CURL_STATICLIB
#endif
#include "../third_party/curl/include/curl/curl.h"

/* curl_easy_impersonate is not in the public curl headers — declare it here. */
typedef CURLcode (*fn_curl_easy_impersonate)(CURL*, const char* target, int default_headers);

/* -----------------------------------------------------------------------
 * Typed setopt / getinfo / share_setopt helpers.
 *
 * curl_easy_setopt is variadic. Calling a variadic function through a
 * generic function pointer is undefined behaviour. Instead, we cast the
 * single resolved symbol into typed non-variadic prototypes per value
 * category and call the matching one.
 * --------------------------------------------------------------------- */

typedef CURLcode (*fn_setopt_long )(CURL*, CURLoption, long);
typedef CURLcode (*fn_setopt_ptr  )(CURL*, CURLoption, const void*);
typedef CURLcode (*fn_setopt_off  )(CURL*, CURLoption, curl_off_t);
typedef CURLcode (*fn_setopt_func )(CURL*, CURLoption, void (*)(void));

typedef CURLcode (*fn_getinfo_long)(CURL*, CURLINFO, long*);
typedef CURLcode (*fn_getinfo_ptr )(CURL*, CURLINFO, void**);

typedef CURLSHcode (*fn_share_setopt_int)(CURLSH*, CURLSHoption, int);
typedef CURLSHcode (*fn_share_setopt_ptr)(CURLSH*, CURLSHoption, void*);

/* -----------------------------------------------------------------------
 * Full symbol table — every curl symbol we resolve at runtime.
 * --------------------------------------------------------------------- */

typedef struct cidn_sym_table_s {
    /* global */
    CURLcode  (*global_init)(long);
    void      (*global_cleanup)(void);

    /* easy */
    CURL*     (*easy_init)(void);
    void*     easy_setopt_raw;   /* cast to fn_setopt_* before use */
    CURLcode  (*easy_perform)(CURL*);
    void*     easy_getinfo_raw;  /* cast to fn_getinfo_* before use */
    void      (*easy_cleanup)(CURL*);
    void      (*easy_reset)(CURL*);
    const char* (*easy_strerror)(CURLcode);

    /* slist */
    struct curl_slist* (*slist_append)(struct curl_slist*, const char*);
    void               (*slist_free_all)(struct curl_slist*);

    /* share */
    CURLSH*    (*share_init)(void);
    void*      share_setopt_raw; /* cast to fn_share_setopt_* before use */
    CURLSHcode (*share_cleanup)(CURLSH*);
    const char* (*share_strerror)(CURLSHcode);

    /* free */
    void (*curl_free)(void*);

    /* impersonate (extra export) */
    fn_curl_easy_impersonate easy_impersonate;
} cidn_sym_table_t;

/* Convenience typed-cast accessors */
static inline CURLcode cidn_setopt_long(const cidn_sym_table_t* s, CURL* h, CURLoption o, long v) {
    return ((fn_setopt_long)s->easy_setopt_raw)(h, o, v);
}
static inline CURLcode cidn_setopt_ptr(const cidn_sym_table_t* s, CURL* h, CURLoption o, const void* v) {
    return ((fn_setopt_ptr)s->easy_setopt_raw)(h, o, v);
}
static inline CURLcode cidn_setopt_off(const cidn_sym_table_t* s, CURL* h, CURLoption o, curl_off_t v) {
    return ((fn_setopt_off)s->easy_setopt_raw)(h, o, v);
}
static inline CURLcode cidn_setopt_func(const cidn_sym_table_t* s, CURL* h, CURLoption o, void (*fn)(void)) {
    return ((fn_setopt_func)s->easy_setopt_raw)(h, o, fn);
}
static inline CURLcode cidn_getinfo_long(const cidn_sym_table_t* s, CURL* h, CURLINFO i, long* out) {
    return ((fn_getinfo_long)s->easy_getinfo_raw)(h, i, out);
}