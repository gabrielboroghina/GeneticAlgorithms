var app = angular.module('ngapp', []);
var flist,t;
var editor = [];

app.controller('tabshow', function () {
    var ctrl = this;
    ctrl.n = 0;
    ctrl.files = [{ name: "", nr: 0 }];
    ctrl.files.pop();
    ctrl.add = function () {
        var newfile = prompt("Enter the new file name (with extension)", "example.txt");
        var res = newfile.split(".");
        label = res[0]+res[1];
        if (label!=null)
        { 
            ctrl.n++;
            ctrl.files.push({ name: label, nr: ctrl.n-1, e:ctrl.n-1, fullname:newfile});
            $('div#tab0').after('<div class="tab-pane" id="tab' + label + '"><div id="editor' + label + '"></div></div>');
            editor[ctrl.n-1] = ace.edit("editor"+ label);
            editor[ctrl.n-1].setTheme("ace/theme/monokai");
            editor[ctrl.n - 1].setShowPrintMargin(false);
            flist = ctrl.files; t = ctrl.n;
        }
    };

    ctrl.remove = function (i,dellabel) {
        if (confirm("Are you sure?")) {
            for (var j = i; j < ctrl.n-1; j++)
                ctrl.files[j] = ctrl.files[j + 1];
            for (var j = 0; j < ctrl.n; j++)
                ctrl.files[j].nr = j;
            ctrl.files.pop();
            $('div#tab' + dellabel).remove();
            ctrl.n--;
            flist = ctrl.files; t = ctrl.n;
        }
    };
});