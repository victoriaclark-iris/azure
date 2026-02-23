window.easyAuth = {
    signOutAndRedirect: function (targetUrl) {
        // Construct the absolute redirect URL
        var redirectUrl = window.location.origin + (targetUrl || '/signed-out');
        
        // Try the logout with post_logout_redirect_uri first
        try {
            var logoutUrl = '/.auth/logout?post_logout_redirect_uri=' + encodeURIComponent(redirectUrl);
            console.log('Attempting logout with URL:', logoutUrl);
            
            // Use fetch instead of Image for better error handling
            fetch(logoutUrl, { 
                method: 'GET',
                credentials: 'same-origin'
            }).then(function(response) {
                if (response.ok || response.redirected) {
                    // If successful or redirected, go to target
                    window.location.href = redirectUrl;
                } else {
                    // If failed, try simple logout without redirect parameter
                    console.warn('Logout with redirect failed, trying simple logout');
                    window.location.href = '/.auth/logout';
                }
            }).catch(function(error) {
                console.error('Logout error:', error);
                // Fallback to simple logout
                window.location.href = '/.auth/logout';
            });
        } catch (error) {
            console.error('Logout URL construction error:', error);
            // Fallback to simple logout and manual redirect
            window.location.href = '/.auth/logout';
            window.setTimeout(function () {
                window.location.href = redirectUrl;
            }, 1000);
        }
    }
};
