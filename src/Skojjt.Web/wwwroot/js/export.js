// Skojjt Export Helper
// Handles file downloads with error checking for export operations

window.skojjtExport = {
    /**
     * Download a file from the given URL, checking for JSON error responses.
     * @param {string} url - The URL to download from
     * @returns {Promise<{success: boolean, error?: string, message?: string, configurationUrl?: string}>}
     */
    downloadWithErrorHandling: async function (url) {
        try {
            const response = await fetch(url, {
                method: 'GET',
                credentials: 'include', // Include auth cookies
                headers: {
                    'Accept': '*/*'
                }
            });

            // Check if the response is an error
            if (!response.ok) {
                const contentType = response.headers.get('content-type');
                
                // If it's JSON, parse the error
                if (contentType && contentType.includes('application/json')) {
                    const errorData = await response.json();
                    return {
                        success: false,
                        error: errorData.error || 'Exportfel',
                        message: errorData.message || `HTTP ${response.status}`,
                        configurationUrl: errorData.configurationUrl || null
                    };
                }
                
                // Otherwise, return a generic error
                return {
                    success: false,
                    error: 'Exportfel',
                    message: `HTTP ${response.status}: ${response.statusText}`,
                    configurationUrl: null
                };
            }

            // Success - trigger the download
            const blob = await response.blob();
            
            // Extract filename from Content-Disposition header or use a default
            let filename = 'export';
            const disposition = response.headers.get('content-disposition');
            if (disposition) {
                const filenameMatch = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
                if (filenameMatch && filenameMatch[1]) {
                    filename = filenameMatch[1].replace(/['"]/g, '');
                    // Handle UTF-8 encoded filenames
                    if (filename.startsWith("UTF-8''")) {
                        filename = decodeURIComponent(filename.substring(7));
                    }
                }
            }

            // Create download link
            const downloadUrl = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = downloadUrl;
            link.download = filename;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.URL.revokeObjectURL(downloadUrl);

            return {
                success: true,
                error: null,
                message: null,
                configurationUrl: null
            };
        } catch (error) {
            console.error('Export download error:', error);
            return {
                success: false,
                error: 'Nätverksfel',
                message: error.message || 'Kunde inte ladda ner filen',
                configurationUrl: null
            };
        }
    },

    /**
     * Download a file from a byte array (base64-encoded).
     * @param {string} filename - The filename for the download
     * @param {string} contentType - The MIME type
     * @param {string} base64 - The base64-encoded file content
     */
    downloadFileFromBase64: function (filename, contentType, base64) {
        const byteCharacters = atob(base64);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: contentType });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
    }
};
