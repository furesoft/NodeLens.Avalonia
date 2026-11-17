<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="3bc52e4d-b3b5-490e-9456-afad0bdeb6c7" name="pipeline">
  <nodes>
    <node id="9c4dcf01-2b0b-4f30-820b-4b855f071c62" concept="Pipeline.Pipeline" name="Release">
      <properties>
        <name>Release</name>
      </properties>
      <children role="stages">
        <node id="fa4086e1-251c-4d59-bdac-9e1af90c369e" concept="Pipeline.Stage" name="build">
          <properties>
            <name>build</name>
            <command>dotnet build</command>
            <retries>1</retries>
          </properties>
        </node>
        <node id="8b9808e3-a4f9-4ac9-a22e-d3e7f825735f" concept="Pipeline.Stage" name="test">
          <properties>
            <name>test</name>
            <command>dotnet test</command>
            <retries>0</retries>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>