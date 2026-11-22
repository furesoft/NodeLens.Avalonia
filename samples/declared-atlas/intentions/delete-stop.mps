<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b3000000-0000-4000-9005-000000000002" name="delete-stop">
  <nodes>
    <node id="b3000000-0000-4000-8005-000000000002" concept="Intent.Rule" name="Delete this stop">
      <properties>
        <conceptKey>Atlas.Stop</conceptKey>
        <label>Delete this stop</label>
        <description>Removes the stop, so the route goes on from the one before it.</description>
      </properties>
      <children role="action">
        <node id="b3000000-0000-4000-8005-000000000102" concept="Intent.Delete" />
      </children>
    </node>
  </nodes>
</model>