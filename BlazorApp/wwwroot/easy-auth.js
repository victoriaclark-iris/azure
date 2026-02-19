window.easyAuth = {
    signOutAndRedirect: function (targetUrl) {
        var logoutRequest = new Image();
        logoutRequest.src = '/.auth/logout?post_logout_redirect_uri=' + encodeURIComponent(window.location.origin + '/signed-out');

        window.setTimeout(function () {
            window.location.href = targetUrl || '/signed-out';
        }, 900);
    }
};
