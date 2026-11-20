<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="1362de55-1603-4c56-acc4-46df93923782" name="reads">
  <nodes>
    <node id="719a949a-20ae-44b0-a03d-425eb1408fc2" concept="Testing.Test" name="variant.ini">
      <properties>
        <document>variant.ini</document>
      </properties>
      <children role="assertion">
        <node id="c550ccfd-7134-4ecf-a1ce-f433f15e0344" concept="Testing.Reads" name="ini">
          <properties>
            <importer>ini</importer>
            <generator>ShellScript</generator>
          </properties>
          <children role="lines">
            <node id="978146f3-2192-4e27-a9f0-474c2a8744d6" concept="Testing.Line" name="# Release">
              <properties>
                <text># Release</text>
              </properties>
            </node>
            <node id="0642cf19-6d7d-4efd-8599-86706c1f10d9" concept="Testing.Line" name="dotnet build">
              <properties>
                <text>dotnet build</text>
              </properties>
            </node>
            <node id="db19bb05-f1fc-4a3a-aebb-5d9cfcaff159" concept="Testing.Line" name="dotnet test">
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