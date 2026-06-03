#include "../include/cidn_wrapper.h"
#include "cidn_loader.h"

#include <stdlib.h>
#include <string.h>
#include <stdio.h>

#ifdef _WIN32
  #define WIN32_LEAN_AND_MEAN
  #include <windows.h>
  static __declspec(thread) char tl_error[512];
  typedef CRITICAL_SECTION cidn_mutex_t;
  static void mutex_init(cidn_mutex_t* m)    { InitializeCriticalSection(m); }
  static void mutex_lock(cidn_mutex_t* m)    { EnterCriticalSection(m); }
  static void mutex_unlock(cidn_mutex_t* m)  { LeaveCriticalSection(m); }
  static void mutex_destroy(cidn_mutex_t* m) { DeleteCriticalSection(m); }
#else
  #include <pthread.h>
  static __thread char tl_error[512];
  typedef pthread_mutex_t cidn_mutex_t;
  static void mutex_init(cidn_mutex_t* m)    { pthread_mutex_init(m, NULL); }
  static void mutex_lock(cidn_mutex_t* m)    { pthread_mutex_lock(m); }
  static void mutex_unlock(cidn_mutex_t* m)  { pthread_mutex_unlock(m); }
  static void mutex_destroy(cidn_mutex_t* m) { pthread_mutex_destroy(m); }
#endif

static void set_error(const char* msg) {
    strncpy(tl_error, msg, sizeof(tl_error) - 1);
    tl_error[sizeof(tl_error) - 1] = '\0';
}

const char* cidn_last_error(void) { return tl_error; }

/* -----------------------------------------------------------------------
 * Lifecycle
 * --------------------------------------------------------------------- */

int cidn_global_init(const char* libcurl_path) {
    cidn_sym_table_t table;
    if (cidn_loader_load(libcurl_path, &table) != 0) {
        set_error(cidn_loader_last_error());
        return -1;
    }
    CURLcode rc = table.global_init(CURL_GLOBAL_ALL);
    if (rc != CURLE_OK) {
        snprintf(tl_error, sizeof(tl_error),
                 "curl_global_init failed: %s", table.easy_strerror(rc));
        return (int)rc;
    }
    return 0;
}

void cidn_global_cleanup(void) {
    const cidn_sym_table_t* s = cidn_loader_symbols();
    if (s) s->global_cleanup();
    cidn_loader_unload();
}

const char* cidn_curl_strerror(int code) {
    const cidn_sym_table_t* s = cidn_loader_symbols();
    if (!s) return "library not loaded";
    return s->easy_strerror((CURLcode)code);
}

/* -----------------------------------------------------------------------
 * Session
 * --------------------------------------------------------------------- */

typedef struct cidn_session_s {
    CURLSH*       share;
    cidn_mutex_t  cookie_mutex;
    cidn_mutex_t  connect_mutex;
    cidn_mutex_t  dns_mutex;
    cidn_mutex_t  ssl_mutex;
} cidn_session_impl_t;

static void share_lock_cb(CURL* handle, curl_lock_data data,
                          curl_lock_access access, void* userptr) {
    (void)handle; (void)access;
    cidn_session_impl_t* sess = (cidn_session_impl_t*)userptr;
    switch (data) {
        case CURL_LOCK_DATA_COOKIE:    mutex_lock(&sess->cookie_mutex);  break;
        case CURL_LOCK_DATA_CONNECT:   mutex_lock(&sess->connect_mutex); break;
        case CURL_LOCK_DATA_DNS:       mutex_lock(&sess->dns_mutex);     break;
        case CURL_LOCK_DATA_SSL_SESSION: mutex_lock(&sess->ssl_mutex);   break;
        default: break;
    }
}

static void share_unlock_cb(CURL* handle, curl_lock_data data, void* userptr) {
    (void)handle;
    cidn_session_impl_t* sess = (cidn_session_impl_t*)userptr;
    switch (data) {
        case CURL_LOCK_DATA_COOKIE:    mutex_unlock(&sess->cookie_mutex);  break;
        case CURL_LOCK_DATA_CONNECT:   mutex_unlock(&sess->connect_mutex); break;
        case CURL_LOCK_DATA_DNS:       mutex_unlock(&sess->dns_mutex);     break;
        case CURL_LOCK_DATA_SSL_SESSION: mutex_unlock(&sess->ssl_mutex);   break;
        default: break;
    }
}

cidn_session_t cidn_session_create(void) {
    const cidn_sym_table_t* s = cidn_loader_symbols();
    if (!s) { set_error("library not loaded"); return NULL; }

    cidn_session_impl_t* sess = calloc(1, sizeof(cidn_session_impl_t));
    if (!sess) { set_error("out of memory"); return NULL; }

    mutex_init(&sess->cookie_mutex);
    mutex_init(&sess->connect_mutex);
    mutex_init(&sess->dns_mutex);
    mutex_init(&sess->ssl_mutex);

    sess->share = s->share_init();
    if (!sess->share) { set_error("curl_share_init failed"); free(sess); return NULL; }

    typedef CURLSHcode (*fn_share_setopt_int_t)(CURLSH*, CURLSHoption, int);
    typedef CURLSHcode (*fn_share_setopt_ptr_t)(CURLSH*, CURLSHoption, void*);

    ((fn_share_setopt_ptr_t)s->share_setopt_raw)(sess->share, CURLSHOPT_LOCKFUNC,   (void*)share_lock_cb);
    ((fn_share_setopt_ptr_t)s->share_setopt_raw)(sess->share, CURLSHOPT_UNLOCKFUNC, (void*)share_unlock_cb);
    ((fn_share_setopt_ptr_t)s->share_setopt_raw)(sess->share, CURLSHOPT_USERDATA,   sess);
    ((fn_share_setopt_int_t)s->share_setopt_raw)(sess->share, CURLSHOPT_SHARE, CURL_LOCK_DATA_COOKIE);
    ((fn_share_setopt_int_t)s->share_setopt_raw)(sess->share, CURLSHOPT_SHARE, CURL_LOCK_DATA_CONNECT);
    ((fn_share_setopt_int_t)s->share_setopt_raw)(sess->share, CURLSHOPT_SHARE, CURL_LOCK_DATA_DNS);
    ((fn_share_setopt_int_t)s->share_setopt_raw)(sess->share, CURLSHOPT_SHARE, CURL_LOCK_DATA_SSL_SESSION);

    return (cidn_session_t)sess;
}

void cidn_session_destroy(cidn_session_t session) {
    if (!session) return;
    cidn_session_impl_t* sess = (cidn_session_impl_t*)session;
    const cidn_sym_table_t* s = cidn_loader_symbols();
    if (s && sess->share) s->share_cleanup(sess->share);
    mutex_destroy(&sess->cookie_mutex);
    mutex_destroy(&sess->connect_mutex);
    mutex_destroy(&sess->dns_mutex);
    mutex_destroy(&sess->ssl_mutex);
    free(sess);
}

/* -----------------------------------------------------------------------
 * Request
 * --------------------------------------------------------------------- */

typedef struct cidn_request_s {
    char*              method;
    char*              url;
    struct curl_slist* headers;
    uint8_t*           body;
    size_t             body_len;
    char*              impersonate_target;
    int                impersonate_default_headers;
    char*              proxy;
    char*              proxy_userpwd;
    int                follow_redirects;
    long               max_redirects;
    long               timeout_ms;
    long               connect_timeout_ms;
    char*              accept_encoding;
    int                verify_peer;
    int                verify_host;
    char*              cookie;
    int                cookie_jar_enabled;
    int                verbose;
    volatile int*      abort_flag;
} cidn_request_impl_t;

cidn_request_t cidn_request_create(void) {
    cidn_request_impl_t* r = calloc(1, sizeof(cidn_request_impl_t));
    if (!r) return NULL;
    r->method = strdup("GET");
    r->verify_peer = 1;
    r->verify_host = 2;
    r->follow_redirects = 1;
    r->max_redirects = 10;
    r->impersonate_default_headers = 1;
    return (cidn_request_t)r;
}

void cidn_request_destroy(cidn_request_t req) {
    if (!req) return;
    cidn_request_impl_t* r = (cidn_request_impl_t*)req;
    const cidn_sym_table_t* s = cidn_loader_symbols();
    free(r->method); free(r->url);
    if (s && r->headers) s->slist_free_all(r->headers);
    free(r->body); free(r->impersonate_target);
    free(r->proxy); free(r->proxy_userpwd);
    free(r->accept_encoding); free(r->cookie);
    free(r);
}

#define SET_STR(field) \
    do { free(r->field); r->field = val ? strdup(val) : NULL; return 0; } while(0)

int cidn_request_set_method(cidn_request_t req, const char* val) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req; SET_STR(method);
}
int cidn_request_set_url(cidn_request_t req, const char* val) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req; SET_STR(url);
}
int cidn_request_set_impersonate(cidn_request_t req, const char* target, int default_headers) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req;
    free(r->impersonate_target);
    r->impersonate_target = target ? strdup(target) : NULL;
    r->impersonate_default_headers = default_headers;
    return 0;
}
int cidn_request_set_proxy(cidn_request_t req, const char* proxy, const char* userpwd) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req;
    free(r->proxy); r->proxy = proxy ? strdup(proxy) : NULL;
    free(r->proxy_userpwd); r->proxy_userpwd = userpwd ? strdup(userpwd) : NULL;
    return 0;
}
int cidn_request_set_follow_redirects(cidn_request_t req, int follow, long max_r) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req;
    r->follow_redirects = follow; r->max_redirects = max_r; return 0;
}
int cidn_request_set_timeout_ms(cidn_request_t req, long total_ms, long connect_ms) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req;
    r->timeout_ms = total_ms; r->connect_timeout_ms = connect_ms; return 0;
}
int cidn_request_set_accept_encoding(cidn_request_t req, const char* val) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req; SET_STR(accept_encoding);
}
int cidn_request_set_verify(cidn_request_t req, int peer, int host) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req;
    r->verify_peer = peer; r->verify_host = host ? 2 : 0; return 0;
}
int cidn_request_set_cookie(cidn_request_t req, const char* val) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req; SET_STR(cookie);
}
int cidn_request_set_cookie_jar_enabled(cidn_request_t req, int enabled) {
    ((cidn_request_impl_t*)req)->cookie_jar_enabled = enabled; return 0;
}
int cidn_request_set_verbose(cidn_request_t req, int verbose) {
    ((cidn_request_impl_t*)req)->verbose = verbose; return 0;
}

int cidn_request_set_abort_flag(cidn_request_t req, volatile int* flag) {
    ((cidn_request_impl_t*)req)->abort_flag = flag; return 0;
}

static int abort_progress_cb(void* userdata, curl_off_t dt, curl_off_t dn,
                              curl_off_t ut, curl_off_t un) {
    (void)dt; (void)dn; (void)ut; (void)un;
    return *(volatile int*)userdata ? 1 : 0;
}

int cidn_request_add_header(cidn_request_t req, const char* header) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req;
    const cidn_sym_table_t* s = cidn_loader_symbols();
    if (!s) return -1;
    r->headers = s->slist_append(r->headers, header);
    return r->headers ? 0 : -1;
}

int cidn_request_set_body(cidn_request_t req, const uint8_t* data, size_t len) {
    cidn_request_impl_t* r = (cidn_request_impl_t*)req;
    free(r->body);
    if (data && len > 0) {
        r->body = malloc(len);
        if (!r->body) return -1;
        memcpy(r->body, data, len);
        r->body_len = len;
    } else {
        r->body = NULL; r->body_len = 0;
    }
    return 0;
}

/* -----------------------------------------------------------------------
 * Shared easy handle setup
 * --------------------------------------------------------------------- */

static CURL* setup_easy(const cidn_sym_table_t* s, cidn_session_impl_t* sess,
                        cidn_request_impl_t* req) {
    CURL* h = s->easy_init();
    if (!h) { set_error("curl_easy_init failed"); return NULL; }

    /* Bind to session share */
    cidn_setopt_ptr(s, h, CURLOPT_SHARE, sess->share);

    /* Impersonate — must be called before other setopt calls */
    if (req->impersonate_target) {
        CURLcode rc = s->easy_impersonate(h, req->impersonate_target,
                                           req->impersonate_default_headers);
        if (rc != CURLE_OK) {
            snprintf(tl_error, sizeof(tl_error), "curl_easy_impersonate: %s",
                     s->easy_strerror(rc));
            s->easy_cleanup(h);
            return NULL;
        }
    }

    cidn_setopt_ptr (s, h, CURLOPT_URL,            req->url);
    cidn_setopt_ptr (s, h, CURLOPT_CUSTOMREQUEST,  req->method);
    cidn_setopt_long(s, h, CURLOPT_FOLLOWLOCATION, req->follow_redirects);
    cidn_setopt_long(s, h, CURLOPT_MAXREDIRS,      req->max_redirects);
    cidn_setopt_long(s, h, CURLOPT_TIMEOUT_MS,     req->timeout_ms);
    cidn_setopt_long(s, h, CURLOPT_CONNECTTIMEOUT_MS, req->connect_timeout_ms);
    cidn_setopt_long(s, h, CURLOPT_SSL_VERIFYPEER, req->verify_peer);
    cidn_setopt_long(s, h, CURLOPT_SSL_VERIFYHOST, req->verify_host);
    cidn_setopt_long(s, h, CURLOPT_VERBOSE,        req->verbose);

    if (req->headers)
        cidn_setopt_ptr(s, h, CURLOPT_HTTPHEADER, req->headers);
    if (req->accept_encoding)
        cidn_setopt_ptr(s, h, CURLOPT_ACCEPT_ENCODING, req->accept_encoding);
    if (req->proxy)
        cidn_setopt_ptr(s, h, CURLOPT_PROXY, req->proxy);
    if (req->proxy_userpwd)
        cidn_setopt_ptr(s, h, CURLOPT_PROXYUSERPWD, req->proxy_userpwd);
    if (req->cookie)
        cidn_setopt_ptr(s, h, CURLOPT_COOKIE, req->cookie);
    if (req->cookie_jar_enabled)
        cidn_setopt_ptr(s, h, CURLOPT_COOKIEFILE, ""); /* enables cookie engine */

    if (req->abort_flag) {
        cidn_setopt_func(s, h, CURLOPT_XFERINFOFUNCTION, (void(*)(void))abort_progress_cb);
        cidn_setopt_ptr (s, h, CURLOPT_XFERINFODATA,     (void*)req->abort_flag);
        cidn_setopt_long(s, h, CURLOPT_NOPROGRESS,       0L);
    }

    if (req->body && req->body_len > 0) {
        cidn_setopt_ptr(s, h, CURLOPT_POSTFIELDS,   req->body);
        cidn_setopt_off(s, h, CURLOPT_POSTFIELDSIZE_LARGE, (curl_off_t)req->body_len);
    }

    return h;
}

/* -----------------------------------------------------------------------
 * Buffered execute
 * --------------------------------------------------------------------- */

typedef struct { uint8_t* buf; size_t len; size_t cap; } dyn_buf_t;

static size_t write_to_dyn(const char* ptr, size_t sz, size_t nmemb, void* ud) {
    dyn_buf_t* b = (dyn_buf_t*)ud;
    size_t n = sz * nmemb;
    if (b->len + n + 1 > b->cap) {
        size_t new_cap = (b->cap ? b->cap * 2 : 4096);
        while (new_cap < b->len + n + 1) new_cap *= 2;
        uint8_t* nb = realloc(b->buf, new_cap);
        if (!nb) return 0;
        b->buf = nb; b->cap = new_cap;
    }
    memcpy(b->buf + b->len, ptr, n);
    b->len += n;
    b->buf[b->len] = '\0';
    return n;
}

int cidn_session_execute(cidn_session_t session, cidn_request_t req,
                         cidn_response_t* out) {
    memset(out, 0, sizeof(*out));

    const cidn_sym_table_t* s = cidn_loader_symbols();
    if (!s) {
        out->error = strdup("library not loaded");
        return -1;
    }

    cidn_session_impl_t* sess = (cidn_session_impl_t*)session;
    cidn_request_impl_t* r    = (cidn_request_impl_t*)req;

    CURL* h = setup_easy(s, sess, r);
    if (!h) {
        out->error = strdup(tl_error[0] ? tl_error : "setup_easy failed (no detail)");
        fprintf(stderr, "cidn: setup_easy failed: %s\n", out->error);
        return -1;
    }

    dyn_buf_t body    = {0};
    dyn_buf_t headers = {0};

    cidn_setopt_func(s, h, CURLOPT_WRITEFUNCTION,  (void(*)(void))write_to_dyn);
    cidn_setopt_ptr (s, h, CURLOPT_WRITEDATA,      &body);
    cidn_setopt_func(s, h, CURLOPT_HEADERFUNCTION, (void(*)(void))write_to_dyn);
    cidn_setopt_ptr (s, h, CURLOPT_HEADERDATA,     &headers);

    CURLcode rc = s->easy_perform(h);

    if (rc == CURLE_OK) {
        long status = 0;
        cidn_getinfo_long(s, h, CURLINFO_RESPONSE_CODE, &status);
        out->status_code  = status;
        out->body         = body.buf;
        out->body_len     = body.len;
        out->headers_blob = (char*)headers.buf;
        char* eff = NULL;
        ((fn_getinfo_ptr)s->easy_getinfo_raw)(h, CURLINFO_EFFECTIVE_URL, (void**)&eff);
        out->effective_url = eff ? strdup(eff) : NULL;
    } else {
        free(body.buf); free(headers.buf);
        out->error = strdup(s->easy_strerror(rc));
    }

    s->easy_cleanup(h);
    return rc == CURLE_OK ? 0 : (int)rc;
}

void cidn_response_free(cidn_response_t* resp) {
    if (!resp) return;
    free(resp->headers_blob);
    free(resp->body);
    free(resp->effective_url);
    free(resp->error);
    memset(resp, 0, sizeof(*resp));
}

/* -----------------------------------------------------------------------
 * Streaming execute
 * --------------------------------------------------------------------- */

typedef struct {
    cidn_write_cb  write_cb;
    void*          write_ud;
    cidn_header_cb header_cb;
    void*          header_ud;
} stream_ctx_t;

static size_t stream_write(const char* ptr, size_t sz, size_t nmemb, void* ud) {
    stream_ctx_t* ctx = (stream_ctx_t*)ud;
    if (!ctx->write_cb) return sz * nmemb;
    return ctx->write_cb((const uint8_t*)ptr, sz * nmemb, ctx->write_ud);
}

static size_t stream_header(char* ptr, size_t sz, size_t nmemb, void* ud) {
    stream_ctx_t* ctx = (stream_ctx_t*)ud;
    if (!ctx->header_cb) return sz * nmemb;
    return ctx->header_cb(ptr, sz * nmemb, ctx->header_ud);
}

int cidn_session_execute_stream(cidn_session_t session, cidn_request_t req,
                                cidn_write_cb write_cb, void* body_ud,
                                cidn_header_cb header_cb, void* hdr_ud,
                                long* out_status) {
    const cidn_sym_table_t* s = cidn_loader_symbols();
    if (!s) { set_error("library not loaded"); return -1; }

    cidn_session_impl_t* sess = (cidn_session_impl_t*)session;
    cidn_request_impl_t* r    = (cidn_request_impl_t*)req;

    CURL* h = setup_easy(s, sess, r);
    if (!h) return -1;

    stream_ctx_t ctx = { write_cb, body_ud, header_cb, hdr_ud };
    cidn_setopt_func(s, h, CURLOPT_WRITEFUNCTION,  (void(*)(void))stream_write);
    cidn_setopt_ptr (s, h, CURLOPT_WRITEDATA,      &ctx);
    cidn_setopt_func(s, h, CURLOPT_HEADERFUNCTION, (void(*)(void))stream_header);
    cidn_setopt_ptr (s, h, CURLOPT_HEADERDATA,     &ctx);

    CURLcode rc = s->easy_perform(h);
    if (rc == CURLE_OK && out_status) {
        cidn_getinfo_long(s, h, CURLINFO_RESPONSE_CODE, out_status);
    }
    s->easy_cleanup(h);
    return rc == CURLE_OK ? 0 : (int)rc;
}
