<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="c2000000-0000-4000-9000-000000000001" name="route">
  <nodes>
    <node id="c2000000-0000-4000-8000-000000000001" concept="Atlas.Route" name="loop">
      <properties>
        <name>loop</name>
      </properties>
      <children role="stops">
        <node id="c2000000-0000-4000-8000-000000000002" concept="Atlas.Stop">
          <references role="to" target="c1000000-0000-4000-8000-000000000002" resolveInfo="peak" />
        </node>
        <node id="c2000000-0000-4000-8000-000000000003" concept="Atlas.Stop">
          <references role="to" target="c1000000-0000-4000-8000-000000000003" resolveInfo="lake" />
        </node>
      </children>
    </node>
  </nodes>
</model>