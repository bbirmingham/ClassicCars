// global site JS
var main = {

    // interact with the site API
    useAPI: function(data, callback) {
        $.ajax({
            url: data.url,
            method: data.method,
            dataType: 'json',
            data: data
        })
        .done(function (d) {
            callback(d);
        })
        .fail(function () {
            callback({ error: 'Unknown error occurred' });
        });
    },
    // return URL variable
    // based on http://jquery-howto.blogspot.co.uk/2009/09/get-url-parameters-values-with-jquery.html
    getUrlVar: function (name) {
        var vars = [], hash;
        var hashes = window.location.href.slice(window.location.href.indexOf('?') + 1).split('&');

        for (var i = 0; i < hashes.length; i++) {
            hash = hashes[i].split('=');
            vars.push(hash[0]);
            vars[hash[0]] = hash[1];
        }

        return vars[name];
    }
}