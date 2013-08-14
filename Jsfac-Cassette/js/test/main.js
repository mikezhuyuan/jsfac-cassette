// @reference ~/js/jsfac.js
jsfac.module('test', ['util'], function (register) {
    register('main', ['httpClient', 'zepto'], function (httpClient) {
        return 'main';
    });
});