<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="99cd1e96-b4f7-4602-9990-e87a8b7dcfd9" name="reads-crlf">
  <nodes>
    <node id="27956f7d-0255-4bb1-94c0-46aad968017b" concept="Testing.Test" name="variant-crlf.ini">
      <properties>
        <document>variant-crlf.ini</document>
      </properties>
      <children role="assertion">
        <node id="fddd8c62-58ad-4af1-aaf0-c67b4b923e74" concept="Testing.Reads" name="ini">
          <properties>
            <importer>ini</importer>
            <generator>ShellScript</generator>
          </properties>
          <children role="lines">
            <node id="3bd496e2-9c71-47ef-a8d4-e44758462856" concept="Testing.Line" name="# Release">
              <properties>
                <text># Release</text>
              </properties>
            </node>
            <node id="82ee857d-fccf-4913-8a45-7bf26dd1a068" concept="Testing.Line" name="dotnet build">
              <properties>
                <text>dotnet build</text>
              </properties>
            </node>
            <node id="c0c1a5c9-fd6c-4677-9d1c-baf8f349b66c" concept="Testing.Line" name="dotnet test">
              <properties>
                <text>dotnet test</text>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>