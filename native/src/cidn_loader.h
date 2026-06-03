#pragma once

#include "cidn_curl_decls.h"

/* Load the upstream libcurl-impersonate from the given absolute path,
 * resolve all required symbols into *table, and return 0 on success.
 * On failure, the error string is set in the thread-local last-error slot
 * (accessible via cidn_last_error()) and a non-zero value is returned.
 * Missing symbol names are included in the error message. */
int cidn_loader_load(const char* libcurl_path, cidn_sym_table_t* table);

/* Unload the previously loaded library. Safe to call if not loaded. */
void cidn_loader_unload(void);

/* Returns the currently resolved symbol table, or NULL if not loaded. */
const cidn_sym_table_t* cidn_loader_symbols(void);

/* Returns the last error string set by cidn_loader_load, or NULL. */
const char* cidn_loader_last_error(void);