window.easyAuth = {
    signOutAndRedirect: function (targetUrl) {
        // Construct the absolute redirect URL
        var redirectUrl = window.location.origin + (targetUrl || '/signed-out');
        
        console.log('Easy Auth sign-out initiated');
        console.log('Target URL:', redirectUrl);
        console.log('Current location:', window.location.href);
        
        // Check if we're in a production environment with Easy Auth
        if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
            console.warn('Easy Auth sign-out called in local environment - this may not work');
        }
        
        // Try the logout with post_logout_redirect_uri first
        try {
            var logoutUrl = '/.auth/logout?post_logout_redirect_uri=' + encodeURIComponent(redirectUrl);
            console.log('Attempting logout with URL:', logoutUrl);
            
            // Use fetch instead of Image for better error handling
            fetch(logoutUrl, { 
                method: 'GET',
                credentials: 'same-origin'
            }).then(function(response) {
                console.log('Logout response status:', response.status);
                if (response.ok || response.redirected) {
                    console.log('Logout successful, redirecting to:', redirectUrl);
                    window.location.href = redirectUrl;
                } else {
                    // If failed, try simple logout without redirect parameter
                    console.warn('Logout with redirect failed, trying simple logout');
                    window.location.href = '/.auth/logout';
                }
            }).catch(function(error) {
                console.error('Logout error:', error);
                // As absolute fallback, redirect to the target URL
                console.log('Fallback: redirecting directly to signed-out page');
                window.location.href = redirectUrl;
            });
        } catch (error) {
            console.error('Logout URL construction error:', error);
            // Fallback redirect
            console.log('Error fallback: redirecting directly to signed-out page');
            window.location.href = redirectUrl;
        }
    }
};
