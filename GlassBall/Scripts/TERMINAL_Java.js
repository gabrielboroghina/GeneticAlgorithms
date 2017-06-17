jQuery(function ($, undefined) {
    $('#console').terminal(function (command, term) {
        $.post("/Home/jinteraction", { data: command });
        $('#stop').click(function () {
            $.post("/Home/End");
            term.echo("Process aborted.", { finalize: function (div) { div.css("color", "red"); } });
        });
    }, {
        greetings: '',
        name: 'q',
        height: 200,
        prompt: '',
        onInit: function (term) {
            $('#start').click(function () {
                var cpp = escape(jeditor.getValue());
                // Declare a proxy to reference the hub. 
                var chat = $.connection.myHub;
                // Create a function that the hub can call to broadcast messages.
                chat.client.broadcastMessage = function (rez) {
                    if (rez[rez.length - 1] == '>') {
                        term.echo(rez + "\n");
                        term.echo("Process exited.", { finalize: function (div) { div.css("color", "orange"); } });

                        if (t > 0) $.post("/Home/jreadfile", { path: flist[0].fullname }, function (data) { editor[flist[0].e].setValue(data); });
                        if (t > 1) $.post("/Home/jreadfile", { path: flist[1].fullname }, function (data) { editor[flist[1].e].setValue(data); });
                        if (t > 2) $.post("/Home/jreadfile", { path: flist[2].fullname }, function (data) { editor[flist[2].e].setValue(data); });
                        if (t > 3) $.post("/Home/jreadfile", { path: flist[3].fullname }, function (data) { editor[flist[3].e].setValue(data); });
                    }
                    else { if (rez[rez.length - 1] == '\n') rez = rez.substring(0, rez.length - 1); term.echo(rez); }
                };
                $.connection.hub.start().done(function () {

                $.post("/Home/jcompile", { cppval: cpp, id:$.connection.hub.id }, function (rez) {
                    $("#build").html(rez);
                    term.clear();
                    if (rez == "Successful compiling!") {
                        //Save Files
                        for (var i = 0; i < t; i++) {
                            var content = editor[flist[i].e].getValue();
                            $.post("/Home/jwritefile", { path: flist[i].fullname, buffer: content });
                        }

                        term.echo("Process started", { finalize: function (div) { div.css("color", "orange"); } });
                        var data = $("#ind").val();
                        $.post("/Home/jrun", { cppval: cpp, datainput: data }, function (rez) {
                            if (rez[rez.length - 1] == '>') {
                                term.echo(rez + "\n");
                                term.echo("Process exited.", { finalize: function (div) { div.css("color", "orange"); } });

                                if (t > 0) $.post("/Home/jreadfile", { path: flist[0].fullname }, function (data) { editor[flist[0].e].setValue(data); });
                                if (t > 1) $.post("/Home/jreadfile", { path: flist[1].fullname }, function (data) { editor[flist[1].e].setValue(data); });
                                if (t > 2) $.post("/Home/jreadfile", { path: flist[2].fullname }, function (data) { editor[flist[2].e].setValue(data); });
                                if (t > 3) $.post("/Home/jreadfile", { path: flist[3].fullname }, function (data) { editor[flist[3].e].setValue(data); });
                            }
                            else { term.echo(rez); }
                        });
                    }
                    else term.echo("Build errors!!!", { finalize: function (div) { div.css("color", "red"); } });
                });
                }).fail(function () { console.log("Not connected."); });
            });
        }
    });
});