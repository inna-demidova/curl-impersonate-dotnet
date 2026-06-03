#pragma once

#ifdef _WIN32
  #ifdef CIDN_BUILD_DLL
    #define CIDN_API __declspec(dllexport)
  #else
    #define CIDN_API __declspec(dllimport)
  #endif
#else
  #define CIDN_API __attribute__((visibility("default")))
#endif

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* -----------------------------------------------------------------------
 * Lifecycle
 * --------------------------------------------------------------------- */

/* Load libcurl-impersonate from libcurl_path (absolute path).
 * Pass NULL to let the loader try well-known names in the same directory
 * as this wrapper DLL (not recommended; pass explicit path from C#).
 * Returns 0 on success. On failure, cidn_last_error() is set. */
CIDN_API int         cidn_global_init(const char* libcurl_path);
CIDN_API void        cidn_global_cleanup(void);

/* Thread-local last error string (never NULL). */
CIDN_API const char* cidn_last_error(void);

/* Map a CURLcode integer to its string description. */
CIDN_API const char* cidn_curl_strerror(int code);

/* -----------------------------------------------------------------------
 * Session  (CURLSH wrapper — cookie jar + connection + DNS + SSL-session)
 * --------------------------------------------------------------------- */

typedef struct cidn_session_s* cidn_session_t;

CIDN_API cidn_session_t cidn_session_create(void);
CIDN_API void           cidn_session_destroy(cidn_session_t session);

/* -----------------------------------------------------------------------
 * Request builder
 * --------------------------------------------------------------------- */

typedef struct cidn_request_s* cidn_request_t;

CIDN_API cidn_request_t cidn_request_create(void);
CIDN_API void           cidn_request_destroy(cidn_request_t req);

CIDN_API int cidn_request_set_method(cidn_request_t req, const char* method);
CIDN_API int cidn_request_set_url(cidn_request_t req, const char* url);
CIDN_API int cidn_request_add_header(cidn_request_t req, const char* header);
CIDN_API int cidn_request_set_body(cidn_request_t req, const uint8_t* data, size_t len);
CIDN_API int cidn_request_set_impersonate(cidn_request_t req, const char* target, int default_headers);
CIDN_API int cidn_request_set_proxy(cidn_request_t req, const char* proxy, const char* userpwd);
CIDN_API int cidn_request_set_follow_redirects(cidn_request_t req, int follow, long max_redirects);
CIDN_API int cidn_request_set_timeout_ms(cidn_request_t req, long total_ms, long connect_ms);
CIDN_API int cidn_request_set_accept_encoding(cidn_request_t req, const char* encoding);
CIDN_API int cidn_request_set_verify(cidn_request_t req, int peer, int host);
CIDN_API int cidn_request_set_cookie(cidn_request_t req, const char* cookie);
CIDN_API int cidn_request_set_cookie_jar_enabled(cidn_request_t req, int enabled);
CIDN_API int cidn_request_set_verbose(cidn_request_t req, int verbose);

/* Optional pointer to a volatile int abort flag. When *flag becomes non-zero
 * the active transfer is aborted with CURLE_ABORTED_BY_CALLBACK (42).
 * The flag is polled via CURLOPT_XFERINFOFUNCTION — enable CURLOPT_NOPROGRESS=0. */
CIDN_API int cidn_request_set_abort_flag(cidn_request_t req, volatile int* flag);

/* -----------------------------------------------------------------------
 * Buffered response
 * Native-owned memory. Caller MUST call cidn_response_free exactly once.
 * --------------------------------------------------------------------- */

typedef struct cidn_response_s {
    long     status_code;
    char*    headers_blob;  /* all headers concatenated, CRLF-separated */
    uint8_t* body;
    size_t   body_len;
    char*    effective_url;
    char*    error;         /* non-NULL when curl returned an error */
} cidn_response_t;

CIDN_API int  cidn_session_execute(cidn_session_t session, cidn_request_t req, cidn_response_t* out);
CIDN_API void cidn_response_free(cidn_response_t* resp);

/* -----------------------------------------------------------------------
 * Streaming execute
 * write_cb / header_cb return the number of bytes consumed.
 * Returning anything other than `len` aborts the transfer.
 * --------------------------------------------------------------------- */

typedef size_t (*cidn_write_cb)(const uint8_t* data, size_t len, void* userdata);
typedef size_t (*cidn_header_cb)(const char* header, size_t len, void* userdata);

CIDN_API int cidn_session_execute_stream(
    cidn_session_t  session,
    cidn_request_t  req,
    cidn_write_cb   write_cb,
    void*           body_ud,
    cidn_header_cb  header_cb,
    void*           hdr_ud,
    long*           out_status
);

#ifdef __cplusplus
}
#endif