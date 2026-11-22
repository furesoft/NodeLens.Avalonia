<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="c1000000-0000-4000-9000-000000000001" name="map">
  <nodes>
    <node id="c1000000-0000-4000-8000-000000000001" concept="Atlas.Map" name="alpen">
      <properties>
        <name>alpen</name>
      </properties>
      <references role="entry" target="c1000000-0000-4000-8000-000000000003" resolveInfo="lake" />
      <children role="places">
        <node id="c1000000-0000-4000-8000-000000000002" concept="Atlas.Place" name="peak">
          <properties>
            <name>peak</name>
            <at>1500m</at>
          </properties>
        </node>
        <node id="c1000000-0000-4000-8000-000000000003" concept="Atlas.Landmark" name="lake">
          <properties>
            <name>lake</name>
            <at>410m</at>
          </properties>
        </node>
      </children>
      <children role="routes">
        <node id="c1000000-0000-4000-8000-000000000004" concept="Atlas.Route" name="loop">
          <properties>
            <name>loop</name>
          </properties>
          <children role="stops">
            <node id="c1000000-0000-4000-8000-000000000005" concept="Atlas.Stop">
              <references role="to" target="c1000000-0000-4000-8000-000000000002" resolveInfo="peak" />
            </node>
            <node id="c1000000-0000-4000-8000-000000000006" concept="Atlas.Stop">
              <references role="to" target="c1000000-0000-4000-8000-000000000003" resolveInfo="lake" />
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>