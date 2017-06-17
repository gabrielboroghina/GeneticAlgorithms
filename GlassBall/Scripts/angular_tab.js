(function () {
    'use strict';
    angular
        .module('tabsDemoDynamicTabs', ['ngMaterial'])
        .controller('AppCtrl', AppCtrl);
    function AppCtrl($scope, $log) {
        var tabs = [
              { title: 'Source', content: "" },
        ],
            selected = null,
            previous = null;
        $scope.tabs = tabs;
        $scope.selectedIndex = 1;
        $scope.$watch('selectedIndex', function (current, old) {
            previous = selected;
            selected = tabs[current];
            if (old && (old != current)) $log.debug('Goodbye ' + previous.title + '!');
            if (current) $log.debug('Hello ' + selected.title + '!');
        });
        $scope.addTab = function (title, view) {
            view = view || title + " Content View";
            tabs.push({ title: title, content: view, disabled: false });
        };
        $scope.removeTab = function (tab) {
            var index = tabs.indexOf(tab);
            tabs.splice(index, 1);
        };
    }
})();